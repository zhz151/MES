using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.Helpers;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Interfaces.Equipment;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Interfaces.Materials;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.StandardRegister;
using MES.Core.Interfaces.Quality;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Materials;
using WoEntity = MES.Data.Entities.WorkOrder.WorkOrder;
using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Materials;

public class SubcontractOrderService : ISubcontractOrderService
{
    private readonly AppDbContext _context;
    private readonly IPurchaseOrderService _purchaseService;
    private readonly IConfigParameterService _configService;
    private readonly IWorkOrderExecutionService _workOrderExecutionService;
    private readonly ILogger<SubcontractOrderService> _logger;
    private readonly Dictionary<string, Dictionary<string, decimal>> _configMaps = new();
    private readonly IMemoryCache _cache;

    // 空值筛选哨兵（与前端 ExcelFilter/BatchPlans 的 "__EXCEL_FILTER_NULL__" 一致）
    private const string FilterNull = "__EXCEL_FILTER_NULL__";

    public SubcontractOrderService(AppDbContext context, IPurchaseOrderService purchaseService,
        IConfigParameterService configService, IWorkOrderExecutionService workOrderExecutionService,
        ILogger<SubcontractOrderService> logger, IMemoryCache cache)
    {
        _context = context;
        _purchaseService = purchaseService;
        _configService = configService;
        _workOrderExecutionService = workOrderExecutionService;
        _logger = logger;
        _cache = cache;
    }

    private async Task TryRefreshExecutionSummaryAsync(string? sourceWorkOrderNo)
    {
        if (string.IsNullOrWhiteSpace(sourceWorkOrderNo)) return;
        try
        {
            await _workOrderExecutionService.RefreshByWorkOrderNosAsync(new List<string> { sourceWorkOrderNo });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "工单执行状况刷新失败（不影响主流程）: SourceWorkOrderNo={SourceWorkOrderNo}", sourceWorkOrderNo);
        }
    }

    private async Task<decimal> GetConfigAsync(string category, string key, decimal defaultValue)
    {
        if (!_configMaps.TryGetValue(category, out var map))
        {
            map = await _configService.GetConfigMapAsync(category);
            _configMaps[category] = map;
        }
        return map.GetValueOrDefault(key, defaultValue);
    }

    public async Task<PagedResult<SubcontractOrderDto>> GetPagedAsync(SubcontractQueryParams query)
    {
        var queryable = _context.SubcontractOrders
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;

            queryable = queryable.Where(s =>
                s.OrderNo.Contains(kw) ||
                s.ProcessType.Contains(kw) ||
                s.OutMaterialCategory.Contains(kw) ||
                s.OutPlantGrade.Contains(kw) ||
                s.OutSpecification.Contains(kw) ||
                (s.SupplierName != null && s.SupplierName.Contains(kw)) ||
                (s.FurnaceNumber != null && s.FurnaceNumber.Contains(kw)) ||
                (s.Remark != null && s.Remark.Contains(kw)));
        }

        // 状态筛选
        if (!string.IsNullOrEmpty(query.Status) && Enum.TryParse<SubcontractOrderStatus>(query.Status, out var parsedStatus))
        {
            queryable = queryable.Where(s => s.Status == parsedStatus);
        }

        // 下单日期筛选
        if (query.DateFrom.HasValue)
        {
            var from = query.DateFrom.Value.Date;
            queryable = queryable.Where(s => s.OrderDate >= from);
        }
        if (query.DateTo.HasValue)
        {
            var to = query.DateTo.Value.Date.AddDays(1);
            queryable = queryable.Where(s => s.OrderDate < to);
        }

        queryable = queryable.ApplyFilters(query.Filters);

        // 排序
        queryable = query.SortBy?.ToLower() switch
        {
            "orderno" => query.IsDescending
                ? queryable.OrderByDescending(s => s.OrderNo)
                : queryable.OrderBy(s => s.OrderNo),
            "orderdate" => query.IsDescending
                ? queryable.OrderByDescending(s => s.OrderDate)
                : queryable.OrderBy(s => s.OrderDate),
            "processtype" => query.IsDescending
                ? queryable.OrderByDescending(s => s.ProcessType)
                : queryable.OrderBy(s => s.ProcessType),
            "outmaterialcategory" => query.IsDescending
                ? queryable.OrderByDescending(s => s.OutMaterialCategory)
                : queryable.OrderBy(s => s.OutMaterialCategory),
            "outplantgrade" => query.IsDescending
                ? queryable.OrderByDescending(s => s.OutPlantGrade)
                : queryable.OrderBy(s => s.OutPlantGrade),
            "outspecification" => query.IsDescending
                ? queryable.OrderByDescending(s => s.OutSpecification)
                : queryable.OrderBy(s => s.OutSpecification),
            "outquantity" => query.IsDescending
                ? queryable.OrderByDescending(s => s.OutQuantity)
                : queryable.OrderBy(s => s.OutQuantity),
            "outweight" => query.IsDescending
                ? queryable.OrderByDescending(s => s.OutWeight)
                : queryable.OrderBy(s => s.OutWeight),
            "returndeadline" => query.IsDescending
                ? queryable.OrderByDescending(s => s.ReturnDeadline)
                : queryable.OrderBy(s => s.ReturnDeadline),
            "suppliername" => query.IsDescending
                ? queryable.OrderByDescending(s => s.SupplierName ?? "")
                : queryable.OrderBy(s => s.SupplierName ?? ""),
            "status" => query.IsDescending
                ? queryable.OrderByDescending(s => s.Status)
                : queryable.OrderBy(s => s.Status),
            "isforcecompleted" => query.IsDescending
                ? queryable.OrderByDescending(s => s.IsForceCompleted)
                : queryable.OrderBy(s => s.IsForceCompleted),
            "furnacenumber" => query.IsDescending
                ? queryable.OrderByDescending(s => s.FurnaceNumber ?? "")
                : queryable.OrderBy(s => s.FurnaceNumber ?? ""),
            "inquantity" => query.IsDescending
                ? queryable.OrderByDescending(s => s.InQuantity ?? 0)
                : queryable.OrderBy(s => s.InQuantity ?? 0),
            "inweight" => query.IsDescending
                ? queryable.OrderByDescending(s => s.InWeight ?? 0)
                : queryable.OrderBy(s => s.InWeight ?? 0),
            "remark" => query.IsDescending
                ? queryable.OrderByDescending(s => s.Remark ?? "")
                : queryable.OrderBy(s => s.Remark ?? ""),
            _ => query.IsDescending
                ? queryable.OrderByDescending(s => s.CreatedTime)
                : queryable.OrderBy(s => s.CreatedTime)
        };

        var totalCount = await queryable.CountAsync();
        var entityList = await queryable
            .Include(s => s.ReturnItems.OrderBy(r => r.Sequence))
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        var items = entityList.Select(ToDto).ToList();

        // 批量查询实发量（仓库出库汇总）
        var orderNos = items.Select(i => i.OrderNo).ToList();
        var outboundWeights = await _context.OutboundRecords
            .Where(r => r.OutboundType == OutboundType.SubcontractOut
                && r.SourceOrderNo != null
                && orderNos.Contains(r.SourceOrderNo))
            .GroupBy(r => r.SourceOrderNo)
            .Select(g => new { OrderNo = g.Key, Quantity = g.Sum(r => (int?)r.OutboundQuantity), Weight = g.Sum(r => (decimal?)r.OutboundWeight) })
            .ToListAsync();
        var weightMap = outboundWeights.ToDictionary(x => x.OrderNo!, x => (decimal?)x.Weight, StringComparer.OrdinalIgnoreCase);
        var quantityMap = outboundWeights.ToDictionary(x => x.OrderNo!, x => (int?)x.Quantity, StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            item.ActualOutboundWeight = weightMap.GetValueOrDefault(item.OrderNo);
            item.ActualOutboundQuantity = quantityMap.GetValueOrDefault(item.OrderNo);
        }

        // 批量查询退货量（委外单号级，各序号求和）
        if (orderNos.Count > 0)
        {
            var returnSummary = await BuildReturnSummaryAsync(orderNos);
            foreach (var item in items)
            {
                if (returnSummary.TryGetValue(item.OrderNo, out var rs))
                {
                    item.ReturnQuantity = rs.BySequence.Values.Sum(x => x.Quantity);
                    item.ReturnWeight = rs.BySequence.Values.Sum(x => x.Weight);
                }
            }
        }

        return new PagedResult<SubcontractOrderDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<SubcontractOrderDto>> GetAllListAsync()
    {
        var entityList = await _context.SubcontractOrders
            .AsNoTracking()
            .Include(s => s.ReturnItems.OrderBy(r => r.Sequence))
            .OrderBy(s => s.OrderDate)
            .ThenBy(s => s.OrderNo)
            .ToListAsync();

        var items = entityList.Select(ToDto).ToList();

        // 退货量补充（委外单号级，各序号求和）
        var orderNos = items.Select(i => i.OrderNo).ToList();
        if (orderNos.Count > 0)
        {
            var returnSummary = await BuildReturnSummaryAsync(orderNos);
            foreach (var item in items)
            {
                if (returnSummary.TryGetValue(item.OrderNo, out var rs))
                {
                    item.ReturnQuantity = rs.BySequence.Values.Sum(x => x.Quantity);
                    item.ReturnWeight = rs.BySequence.Values.Sum(x => x.Weight);
                }
            }
        }

        return items;
    }

    public async Task<SubcontractOrderDto> GetByIdAsync(int id)
    {
        var entity = await _context.SubcontractOrders
            .AsNoTracking()
            .Include(s => s.ReturnItems.OrderBy(r => r.Sequence))
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("委外单不存在");

        var dto = ToDto(entity);

        // 收集所有 ReturnItem 的 SourceWorkOrderNo，批量查询 WorkOrder
        var woNos = entity.ReturnItems
            .Where(r => !string.IsNullOrEmpty(r.SourceWorkOrderNo))
            .Select(r => r.SourceWorkOrderNo!)
            .Distinct()
            .ToList();

        var workOrders = new Dictionary<string, WoEntity>();
        if (woNos.Count > 0)
        {
            workOrders = await _context.WorkOrders
                .AsNoTracking()
                .Where(w => woNos.Contains(w.WorkOrderNo))
                .ToDictionaryAsync(w => w.WorkOrderNo, w => w);
        }

        // 退货量补充：委外单号级（详情表头「退货总量」）+ 序号级（明细「退货量」）
        var returnSummary = await BuildReturnSummaryAsync(new[] { entity.OrderNo });
        var returnBySequence = returnSummary.TryGetValue(entity.OrderNo, out var rs) ? rs.BySequence : new Dictionary<int, (int Quantity, decimal Weight)>();
        dto.ReturnQuantity = returnBySequence.Values.Sum(x => x.Quantity);
        dto.ReturnWeight = returnBySequence.Values.Sum(x => x.Weight);

        dto.ReturnItems = entity.ReturnItems.Select(r =>
        {
            var itemDto = new SubcontractReturnItemDto
            {
                Id = r.Id,
                SubcontractOrderId = r.SubcontractOrderId,
                Sequence = r.Sequence,
                MaterialCategory = !string.IsNullOrEmpty(r.MaterialCategory) && Enum.TryParse<MaterialType>(r.MaterialCategory, out var rc) ? rc : default,
                PlantGrade = r.PlantGrade,
                ProcessSpecification = r.ProcessSpecification,
                UnitWeight = r.UnitWeight,
                RequiredQuantity = r.RequiredQuantity,
                RequiredWeight = r.RequiredWeight,
                InputMultiple = r.InputMultiple,
                ProcessStatusRemark = r.ProcessStatusRemark,
                Remark = r.Remark,
                ProcessUnitPrice = r.ProcessUnitPrice,
                ProcessTotalAmount = r.ProcessTotalAmount,
                SourceWorkOrderNo = r.SourceWorkOrderNo,
                ReturnedQuantity = r.ReturnedQuantity,
                ReturnedWeight = r.ReturnedWeight,
                ProcessStatus = Enum.TryParse<SubcontractOrderStatus>(r.ProcessStatus, out var ps) ? ps : default,
                IsForceCompleted = r.IsForceCompleted
            };

            // 序号级退货量补充
            if (returnBySequence.TryGetValue(r.Sequence, out var ret))
            {
                itemDto.ReturnQuantity = ret.Quantity;
                itemDto.ReturnWeight = ret.Weight;
            }

            // 按每个 ReturnItem 各自的 SourceWorkOrderNo 填充 Wo* 字段
            if (r.SourceWorkOrderNo != null && workOrders.TryGetValue(r.SourceWorkOrderNo, out var wo))
            {
                FillWorkOrderFields(itemDto, wo);
            }

            return itemDto;
        }).ToList();

        return dto;
    }

    public async Task<SubcontractOrderDto> CreateAsync(CreateSubcontractOrderRequest request)
    {
        if (request.ReturnItems == null || request.ReturnItems.Count == 0)
            throw new BusinessException("至少需要一条委外明细要求");

        // Serializable事务：防止并发读取到相同maxSeq导致唯一键冲突
        SubcontractOrder entity = null!;
        var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        using (transaction)
        {
            try
            {
                var orderNo = await GenerateOrderNoAsync();
                var supplierName = await _context.SupplierProfiles
                    .Where(s => s.Id == request.SupplierId)
                    .Select(s => s.SupplierName)
                    .FirstOrDefaultAsync();

                entity = new SubcontractOrder
                {
                    OrderNo = orderNo,
                    SupplierId = request.SupplierId,
                    SupplierName = supplierName,
                    OrderDate = request.OrderDate,
                    ProcessType = "Piercing",
                    FurnaceNumber = request.FurnaceNumber,
                    OutMaterialCategory = request.OutMaterialCategory.ToString(),
                    OutPlantGrade = request.OutPlantGrade,
                    OutSpecification = request.OutSpecification,
                    OutQuantity = request.OutQuantity,
                    OutWeight = request.OutWeight,
                    ReturnDeadline = request.ReturnDeadline,
                    Remark = request.Remark
                };

                int seq = 1;
                foreach (var item in request.ReturnItems)
                {
                    entity.ReturnItems.Add(new SubcontractReturnItem
                    {
                        Sequence = seq++,
                        MaterialCategory = item.MaterialCategory.ToString(),
                        PlantGrade = item.PlantGrade,
                        ProcessSpecification = item.ProcessSpecification,
                        UnitWeight = item.UnitWeight,
                        RequiredQuantity = item.RequiredQuantity,
                        RequiredWeight = item.RequiredWeight,
                        InputMultiple = item.InputMultiple,
                        ProcessStatusRemark = item.ProcessStatusRemark,
                        Remark = item.Remark,
                        ProcessUnitPrice = item.ProcessUnitPrice,
                        ProcessTotalAmount = item.ProcessTotalAmount,
                        SourceWorkOrderNo = item.SourceWorkOrderNo
                    });
                }

                _context.SubcontractOrders.Add(entity);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        var dto = ToDto(entity);

        foreach (var ri in entity.ReturnItems)
            await TryRefreshExecutionSummaryAsync(ri.SourceWorkOrderNo);

        dto.ReturnItems = entity.ReturnItems.Select(r => new SubcontractReturnItemDto
        {
            Id = r.Id,
            SubcontractOrderId = r.SubcontractOrderId,
            Sequence = r.Sequence,
            MaterialCategory = string.IsNullOrEmpty(r.MaterialCategory) ? default : EnumHelper.TryParse<MaterialType>(r.MaterialCategory) ?? default,
            PlantGrade = r.PlantGrade,
            ProcessSpecification = r.ProcessSpecification,
            UnitWeight = r.UnitWeight,
            RequiredQuantity = r.RequiredQuantity,
            RequiredWeight = r.RequiredWeight,
            InputMultiple = r.InputMultiple,
            ProcessStatusRemark = r.ProcessStatusRemark,
            Remark = r.Remark,
            ProcessUnitPrice = r.ProcessUnitPrice,
            ProcessTotalAmount = r.ProcessTotalAmount,
            SourceWorkOrderNo = r.SourceWorkOrderNo,
            ReturnedQuantity = r.ReturnedQuantity,
            ReturnedWeight = r.ReturnedWeight,
            ProcessStatus = Enum.TryParse<SubcontractOrderStatus>(r.ProcessStatus, out var ps) ? ps : default,
            IsForceCompleted = r.IsForceCompleted
        }).ToList();

        return dto;
    }

    public async Task<SubcontractOrderDto> UpdateAsync(int id, UpdateSubcontractOrderRequest request)
    {
        var entity = await _context.SubcontractOrders
            .Include(s => s.ReturnItems)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("委外单不存在");
        if (entity.Status == SubcontractOrderStatus.Completed)
        {
            // 已完成：仅允许修改明细中的来源工单号
            var itemSeq = 0;
            foreach (var item in request.ReturnItems)
            {
                if (itemSeq < entity.ReturnItems.Count)
                {
                    entity.ReturnItems[itemSeq].SourceWorkOrderNo = item.SourceWorkOrderNo;
                }
                itemSeq++;
            }
        }
        else
        {
            entity.SupplierId = request.SupplierId;
            entity.SupplierName = await _context.SupplierProfiles
                .Where(s => s.Id == request.SupplierId)
                .Select(s => s.SupplierName)
                .FirstOrDefaultAsync();
            entity.ProcessType = "Piercing";
            entity.FurnaceNumber = request.FurnaceNumber ?? entity.FurnaceNumber;
            entity.OutMaterialCategory = request.OutMaterialCategory.ToString();
            entity.OutPlantGrade = request.OutPlantGrade;
            entity.OutSpecification = request.OutSpecification;
            entity.OutQuantity = request.OutQuantity;
            entity.OutWeight = request.OutWeight;
            entity.ReturnDeadline = request.ReturnDeadline ?? entity.ReturnDeadline;
            entity.Remark = request.Remark ?? entity.Remark;

            // 全量替换子表
            _context.SubcontractReturnItems.RemoveRange(entity.ReturnItems);

            int seq = 1;
            foreach (var item in request.ReturnItems)
            {
                entity.ReturnItems.Add(new SubcontractReturnItem
                {
                    Sequence = seq++,
                    MaterialCategory = item.MaterialCategory.ToString(),
                    PlantGrade = item.PlantGrade,
                    ProcessSpecification = item.ProcessSpecification,
                    UnitWeight = item.UnitWeight,
                    RequiredQuantity = item.RequiredQuantity,
                    RequiredWeight = item.RequiredWeight,
                    InputMultiple = item.InputMultiple,
                    ProcessStatusRemark = item.ProcessStatusRemark,
                    Remark = item.Remark,
                    ProcessUnitPrice = item.ProcessUnitPrice,
                    ProcessTotalAmount = item.ProcessTotalAmount,
                    SourceWorkOrderNo = item.SourceWorkOrderNo,
                    IsForceCompleted = item.IsForceCompleted
                });
            }

            // 全量替换子表后重新同步回收数据（防替换丢失已进库的回收支数/重量）
            var batches = await _context.InventoryBatches
                .AsNoTracking()
                .Where(b => b.SourceOrderNo == entity.OrderNo)
                .ToListAsync();
            var overRatio = await GetConfigAsync("WarehouseThreshold", "SubcontractOverRatio", 1.05m);
            var overDeviation = await GetConfigAsync("WarehouseThreshold", "SubcontractOverDeviation", 100m);
            // 退货量（序号级）：状态判定按「净回收 = 回收 - 退货」
            var returnSummary = await BuildReturnSummaryAsync(new[] { entity.OrderNo });
            var returnBySequence = returnSummary.TryGetValue(entity.OrderNo, out var rs) ? rs.BySequence : null;
            var orderReturnWeight = returnBySequence?.Values.Sum(x => x.Weight) ?? 0m;
            entity.InQuantity = batches.Sum(b => b.InitialQuantity);
            entity.InWeight = batches.Sum(b => b.InitialWeight);
            foreach (var item in entity.ReturnItems)
                SubcontractHelper.SyncReturnItemFromBatches(item, batches, overRatio, overDeviation, returnBySequence);
            if (!entity.IsForceCompleted)
                await RecalcSubcontractStatusAsync(entity, orderReturnWeight);
            else
                ForceCompleteAllReturnItems(entity);
        }

        await _context.SaveChangesAsync();

        var dto = ToDto(entity);
        dto.ReturnItems = entity.ReturnItems.OrderBy(r => r.Sequence).Select(r => new SubcontractReturnItemDto
        {
            Id = r.Id,
            SubcontractOrderId = r.SubcontractOrderId,
            Sequence = r.Sequence,
            MaterialCategory = string.IsNullOrEmpty(r.MaterialCategory) ? default : EnumHelper.TryParse<MaterialType>(r.MaterialCategory) ?? default,
            PlantGrade = r.PlantGrade,
            ProcessSpecification = r.ProcessSpecification,
            UnitWeight = r.UnitWeight,
            RequiredQuantity = r.RequiredQuantity,
            RequiredWeight = r.RequiredWeight,
            ProcessStatusRemark = r.ProcessStatusRemark,
            Remark = r.Remark,
            ProcessUnitPrice = r.ProcessUnitPrice,
            ProcessTotalAmount = r.ProcessTotalAmount,
            SourceWorkOrderNo = r.SourceWorkOrderNo,
            ReturnedQuantity = r.ReturnedQuantity,
            ReturnedWeight = r.ReturnedWeight,
            ProcessStatus = Enum.TryParse<SubcontractOrderStatus>(r.ProcessStatus, out var ps) ? ps : default,
            IsForceCompleted = r.IsForceCompleted
        }).ToList();

        foreach (var ri in entity.ReturnItems)
            await TryRefreshExecutionSummaryAsync(ri.SourceWorkOrderNo);

        return dto;
    }

    public async Task SyncAllAsync()
    {
        var orders = await _context.SubcontractOrders
            .Include(s => s.ReturnItems)
            .ToListAsync();

        var orderNos = orders.Select(o => o.OrderNo).ToList();
        var batches = await _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.SourceOrderNo != null && orderNos.Contains(b.SourceOrderNo))
            .ToListAsync();

        // 委外超量回收配置（仿采购订单超量到货判定）
        var overRatio = await GetConfigAsync("WarehouseThreshold", "SubcontractOverRatio", 1.05m);
        var overDeviation = await GetConfigAsync("WarehouseThreshold", "SubcontractOverDeviation", 100m);

        // 退货量（序号级）：状态判定按「净回收 = 回收 - 退货」
        var returnSummary = await BuildReturnSummaryAsync(orderNos);

        foreach (var order in orders)
        {
            // SQL 查询后内存过滤须忽略大小写（SQL 排序规则不区分大小写，C# 默认 == 区分，委外单号手输可能大小写不一）
            var orderBatches = batches.Where(b => string.Equals(b.SourceOrderNo, order.OrderNo, StringComparison.OrdinalIgnoreCase)).ToList();

            order.InQuantity = orderBatches.Sum(b => b.InitialQuantity);
            order.InWeight = orderBatches.Sum(b => b.InitialWeight);

            var returnBySequence = returnSummary.TryGetValue(order.OrderNo, out var rs) ? rs.BySequence : null;
            var orderReturnWeight = returnBySequence?.Values.Sum(x => x.Weight) ?? 0m;

            // 同步每个 ReturnItem 的回收数据
            foreach (var item in order.ReturnItems)
            {
                SubcontractHelper.SyncReturnItemFromBatches(item, orderBatches, overRatio, overDeviation, returnBySequence);
            }

            // 主表强制完成 → 子表全部强制完成
            if (order.IsForceCompleted)
                ForceCompleteAllReturnItems(order);
            else
                await RecalcSubcontractStatusAsync(order, orderReturnWeight);
        }

        await _context.SaveChangesAsync();

        foreach (var order in orders)
            foreach (var item in order.ReturnItems)
                await TryRefreshExecutionSummaryAsync(item.SourceWorkOrderNo);
    }

    public async Task SyncSingleAsync(int id)
    {
        var order = await _context.SubcontractOrders
            .Include(s => s.ReturnItems)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (order == null) throw new BusinessException("委外单不存在");

        var batches = await _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.SourceOrderNo == order.OrderNo)
            .ToListAsync();

        order.InQuantity = batches.Sum(b => b.InitialQuantity);
        order.InWeight = batches.Sum(b => b.InitialWeight);

        // 委外超量回收配置（仿采购订单超量到货判定）
        var overRatio = await GetConfigAsync("WarehouseThreshold", "SubcontractOverRatio", 1.05m);
        var overDeviation = await GetConfigAsync("WarehouseThreshold", "SubcontractOverDeviation", 100m);

        // 退货量（序号级）：状态判定按「净回收 = 回收 - 退货」
        var returnSummary = await BuildReturnSummaryAsync(new[] { order.OrderNo });
        var returnBySequence = returnSummary.TryGetValue(order.OrderNo, out var rs) ? rs.BySequence : null;
        var orderReturnWeight = returnBySequence?.Values.Sum(x => x.Weight) ?? 0m;

        // 同步每个 ReturnItem 的回收数据
        foreach (var item in order.ReturnItems)
        {
            SubcontractHelper.SyncReturnItemFromBatches(item, batches, overRatio, overDeviation, returnBySequence);
        }

        // 主表强制完成 → 子表全部强制完成
        if (order.IsForceCompleted)
            ForceCompleteAllReturnItems(order);
        else
            await RecalcSubcontractStatusAsync(order, orderReturnWeight);

        await _context.SaveChangesAsync();

        foreach (var item in order.ReturnItems)
            await TryRefreshExecutionSummaryAsync(item.SourceWorkOrderNo);
    }

public async Task UpdateStatusAsync(int id, UpdateOrderStatusRequest request)
    {
        var entity = await _context.SubcontractOrders
            .Include(s => s.ReturnItems)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("委外单不存在");

        entity.IsForceCompleted = request.IsForceCompleted;

        if (entity.IsForceCompleted)
        {
            entity.Status = SubcontractOrderStatus.Completed;
            // 级联：主表强制完成 → 子表全部强制完成
            ForceCompleteAllReturnItems(entity);
        }
        else
        {
            // 委外超量回收配置（仿采购订单超量到货判定）
            var overRatio = await GetConfigAsync("WarehouseThreshold", "SubcontractOverRatio", 1.05m);
            var overDeviation = await GetConfigAsync("WarehouseThreshold", "SubcontractOverDeviation", 100m);
            // 退货量（序号级）：状态判定按「净回收 = 回收 - 退货」
            var returnSummary = await BuildReturnSummaryAsync(new[] { entity.OrderNo });
            var returnBySequence = returnSummary.TryGetValue(entity.OrderNo, out var rs) ? rs.BySequence : null;
            var orderReturnWeight = returnBySequence?.Values.Sum(x => x.Weight) ?? 0m;

            await RecalcSubcontractStatusAsync(entity, orderReturnWeight);
            // 取消级联：每个子表按实际净回收数据重新计算
            foreach (var item in entity.ReturnItems)
            {
                item.IsForceCompleted = false;
                var returnQuantity = 0;
                var returnWeight = 0m;
                if (returnBySequence != null && returnBySequence.TryGetValue(item.Sequence, out var ret))
                {
                    returnQuantity = ret.Quantity;
                    returnWeight = ret.Weight;
                }
                SubcontractHelper.RecalcReturnItemStatus(item, overRatio, overDeviation, returnQuantity, returnWeight);
            }
        }

        await _context.SaveChangesAsync();

        foreach (var item in entity.ReturnItems)
            await TryRefreshExecutionSummaryAsync(item.SourceWorkOrderNo);
    }

    private static void ForceCompleteAllReturnItems(SubcontractOrder order)
    {
        foreach (var item in order.ReturnItems)
        {
            item.IsForceCompleted = true;
            item.ProcessStatus = SubcontractOrderStatus.Completed.ToString();
        }
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.SubcontractOrders
            .Include(s => s.ReturnItems)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("委外单不存在");
        if (entity.Status == SubcontractOrderStatus.Completed) throw new BusinessException("已完成的委外单无法删除");

        var woNos = entity.ReturnItems
            .Select(r => r.SourceWorkOrderNo)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Distinct()
            .ToList();
        _context.SubcontractOrders.Remove(entity);
        await _context.SaveChangesAsync();
        foreach (var woNo in woNos) await TryRefreshExecutionSummaryAsync(woNo);
    }

    // ========== 用料计划执行状态 ==========

    public async Task<List<ProcurementStatusDto>> GetProcurementStatusAsync()
    {
        return await _purchaseService.GetPiercingProcurementStatusAsync();
    }

    public async Task<List<OrderMismatchInfo>> GetMismatchedSubcontractOrdersAsync()
    {
        // 1. 获取所有涉及采购的工单号
        var semiWoIds = await _context.PurchaseSemiPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0)
            .Select(p => p.WorkOrderId)
            .Distinct()
            .ToListAsync();

        var finishWoIds = await _context.PurchaseFinishedPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0)
            .Select(p => p.WorkOrderId)
            .Distinct()
            .ToListAsync();

        var allWoIds = semiWoIds.Union(finishWoIds).ToList();
        if (allWoIds.Count == 0)
            return new List<OrderMismatchInfo>();

        var validWorkOrderNos = (await _context.WorkOrders
            .AsNoTracking()
            .Where(w => allWoIds.Contains(w.Id))
            .Select(w => w.WorkOrderNo)
            .ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 2. 查询委外明细中 SourceWorkOrderNo 不为空的记录，关联主表获取委外单号
        var mismatchedItems = await _context.SubcontractReturnItems
            .AsNoTracking()
            .Where(r => r.SourceWorkOrderNo != null && r.SourceWorkOrderNo != "")
            .Select(r => new { r.SubcontractOrderId, r.SourceWorkOrderNo })
            .ToListAsync();

        // 3. 获取委外单号映射
        var subcontractIds = mismatchedItems.Select(r => r.SubcontractOrderId).Distinct().ToList();
        var orderNoMap = await _context.SubcontractOrders
            .AsNoTracking()
            .Where(o => subcontractIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.OrderNo);

        // 4. 找出不匹配的，按委外单号分组
        var mismatches = mismatchedItems
            .Where(r => !validWorkOrderNos.Contains(r.SourceWorkOrderNo!))
            .GroupBy(r => r.SubcontractOrderId)
            .Select(g => new OrderMismatchInfo
            {
                OrderNo = orderNoMap.GetValueOrDefault(g.Key, ""),
                MismatchedWorkOrderNos = g.Select(r => r.SourceWorkOrderNo!).Distinct().ToList()
            })
            .Where(m => !string.IsNullOrEmpty(m.OrderNo))
            .ToList();

        return mismatches;
    }

    public async Task<PlanDetailDto?> GetPlanDetailAsync(string workOrderNo, string materialCategory)
    {
        return await _purchaseService.GetPlanDetailAsync(workOrderNo, materialCategory);
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("SubcontractOrderService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var query = from s in _context.SubcontractOrders.AsNoTracking()
                        select new
                        {
                            s.OrderNo,
                            s.OrderDate,
                            s.ProcessType,
                            s.OutMaterialCategory,
                            s.OutPlantGrade,
                            s.OutSpecification,
                            s.ReturnDeadline,
                            s.SupplierName
                        };

            var all = await query.ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["OrderNo"] = all.Select(x => x.OrderNo).Distinct().OrderBy(x => x).ToList(),
                ["OrderDate"] = all.Select(x => x.OrderDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["ProcessType"] = all.Select(x => x.ProcessType).Distinct().OrderBy(x => x).ToList(),
                ["OutMaterialCategory"] = all.Select(x => x.OutMaterialCategory).Distinct().OrderBy(x => x).ToList(),
                ["OutPlantGrade"] = all.Select(x => x.OutPlantGrade).Distinct().OrderBy(x => x).ToList(),
                ["OutSpecification"] = all.Select(x => x.OutSpecification).Distinct().OrderBy(x => x).ToList(),
                ["ReturnDeadline"] = all.Where(x => x.ReturnDeadline != null).Select(x => x.ReturnDeadline!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["SupplierName"] = all.Where(x => x.SupplierName != null).Select(x => x.SupplierName!).Distinct().OrderBy(x => x).ToList(),
            };

        }) ?? new Dictionary<string, List<string>>();
    }

    // ========== 子项执行查询 ==========

    public async Task<PagedResult<SubcontractReturnItemListDto>> GetReturnItemListAsync(QueryParams query, string? status = null)
    {
        var queryable = _context.SubcontractReturnItems
            .AsNoTracking()
            .AsQueryable();

        // 关键字搜索（SQL 层保留，减少加载量）
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(i =>
                (i.OrderNo != null && i.OrderNo.Contains(kw)) ||
                (i.SourceWorkOrderNo != null && i.SourceWorkOrderNo.Contains(kw)) ||
                (i.SubcontractOrder.SupplierName != null && i.SubcontractOrder.SupplierName.Contains(kw)) ||
                (i.PlantGrade != null && i.PlantGrade.Contains(kw)) ||
                i.ProcessSpecification.Contains(kw));
        }

        // 状态筛选（SQL 层保留）
        if (!string.IsNullOrEmpty(status))
            queryable = queryable.Where(i => i.ProcessStatus == status);

        // 全量加载到内存，后续筛选/排序/分页均在内存中完成
        var rawItems = await queryable
            .Select(i => new
            {
                Id = i.Id,
                SubcontractOrderId = i.SubcontractOrderId,
                Sequence = i.Sequence,
                OrderNo = i.OrderNo ?? i.SubcontractOrder.OrderNo,
                SupplierName = i.SubcontractOrder.SupplierName,
                OrderDate = i.SubcontractOrder.OrderDate,
                SourceWorkOrderNo = i.SourceWorkOrderNo,
                PlantGrade = i.PlantGrade,
                ProcessSpecification = i.ProcessSpecification,
                UnitWeight = i.UnitWeight,
                RequiredQuantity = i.RequiredQuantity,
                RequiredWeight = i.RequiredWeight,
                ReturnDeadline = i.SubcontractOrder.ReturnDeadline,
                Remark = i.Remark,
                ReturnedQuantity = i.ReturnedQuantity,
                ReturnedWeight = i.ReturnedWeight,
                IsForceCompleted = i.IsForceCompleted,
                ProcessStatus = i.ProcessStatus
            })
            .ToListAsync();

        var allItems = rawItems.Select(i => new SubcontractReturnItemListDto
        {
            Id = i.Id,
            SubcontractOrderId = i.SubcontractOrderId,
            Sequence = i.Sequence,
            OrderNo = i.OrderNo,
            SupplierName = i.SupplierName,
            OrderDate = i.OrderDate,
            SourceWorkOrderNo = i.SourceWorkOrderNo,
            PlantGrade = i.PlantGrade,
            ProcessSpecification = i.ProcessSpecification,
            UnitWeight = i.UnitWeight,
            RequiredQuantity = i.RequiredQuantity,
            RequiredWeight = i.RequiredWeight,
            RequiredArrivalDate = i.ReturnDeadline,
            Remark = i.Remark,
            ReturnedQuantity = i.ReturnedQuantity,
            ReturnedWeight = i.ReturnedWeight,
            IsForceCompleted = i.IsForceCompleted,
            ProcessStatus = EnumHelper.TryParse<SubcontractOrderStatus>(i.ProcessStatus)
        }).ToList();

        // 退货量（序号级）+ 截止回收日补充（退货出库 ReturnSourceBatchNo → 原仓库批 → SourceOrderNo==委外单号；截止回收日=仓库批 InboundDate 最大值）
        var orderNos = allItems.Select(x => x.OrderNo)
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (orderNos.Count > 0)
        {
            var returnSummary = await BuildReturnSummaryAsync(orderNos);
            foreach (var item in allItems)
            {
                if (item.OrderNo != null && returnSummary.TryGetValue(item.OrderNo, out var rs))
                {
                    if (rs.BySequence.TryGetValue(item.Sequence, out var s))
                    {
                        item.ReturnQuantity = s.Quantity;
                        item.ReturnWeight = s.Weight;
                    }
                    item.ReturnDeadline = rs.LastDate;
                }
            }
        }

        // 工单实时关注：按来源工单号关联工单执行状况读模型（无记录默认 null → 前端 "-"）
        var workOrderNos = allItems.Where(x => !string.IsNullOrEmpty(x.SourceWorkOrderNo))
            .Select(x => x.SourceWorkOrderNo!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (workOrderNos.Count > 0)
        {
            var execLookup = await _context.WorkOrderExecutionSummaries.AsNoTracking()
                .Where(e => workOrderNos.Contains(e.WorkOrderNo))
                .Select(e => new { e.WorkOrderNo, e.ScheduleStage, e.UrgencyLevel, e.RawMaterialLockRemark, e.TheoreticalCutoffDate })
                .ToListAsync();
            var execMap = execLookup.ToDictionary(e => e.WorkOrderNo, e => e, StringComparer.OrdinalIgnoreCase);
            foreach (var item in allItems)
            {
                if (item.SourceWorkOrderNo != null && execMap.TryGetValue(item.SourceWorkOrderNo, out var exec))
                {
                    item.ExecutionScheduleStage = exec.ScheduleStage;
                    item.ExecutionUrgencyLevel = exec.UrgencyLevel;
                    item.ExecutionRawMaterialLockRemark = exec.RawMaterialLockRemark;
                    item.ExecutionTheoreticalCutoffDate = exec.TheoreticalCutoffDate;
                }
            }
        }

        // 内存筛选 — 支持所有 DTO 属性（包括跨表字段如 OrderNo、ReturnDeadline）
        if (query.Filters?.Count > 0)
        {
            var dtoType = typeof(SubcontractReturnItemListDto);
            foreach (var filter in query.Filters)
            {
                var prop = dtoType.GetProperty(filter.Field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null) continue;

                var op = filter.Operator ?? "contains";
                if (op == "isnull")
                {
                    // 空值筛选：仅匹配 null（无工单号/读模型无记录）
                    allItems = allItems.Where(item => prop.GetValue(item) == null).ToList();
                }
                else if (op == "in" && filter.Values?.Count > 0)
                {
                    var filterValues = filter.Values;
                    allItems = allItems.Where(item =>
                    {
                        var val = prop.GetValue(item);
                        if (val == null) return filter.IncludeNull; // 空值记录仅当勾选空值时保留
                        return filterValues.Contains(FormatFilterValue(val), StringComparer.OrdinalIgnoreCase);
                    }).ToList();
                }
                else if (op == "contains" && !string.IsNullOrEmpty(filter.Value))
                {
                    var fv = filter.Value;
                    allItems = allItems.Where(item =>
                    {
                        var val = prop.GetValue(item);
                        if (val == null) return false;
                        return val.ToString()?.Contains(fv, StringComparison.OrdinalIgnoreCase) == true;
                    }).ToList();
                }
                else if (op == "equals" && !string.IsNullOrEmpty(filter.Value))
                {
                    var fv = filter.Value;
                    allItems = allItems.Where(item =>
                    {
                        var val = prop.GetValue(item);
                        if (val == null) return false;
                        return string.Equals(FormatFilterValue(val), fv, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }
                else if (op == "range")
                {
                    allItems = allItems.Where(item =>
                    {
                        var val = prop.GetValue(item);
                        if (val is DateTime dtVal)
                        {
                            if (filter.From is DateTime fromDt && dtVal < fromDt) return false;
                            if (filter.To is DateTime toDt && dtVal > toDt) return false;
                        }
                        return true;
                    }).ToList();
                }
            }
        }

        // 内存排序
        var sorted = (query.SortBy?.ToLower(), query.IsDescending) switch
        {
            ("orderno", true) => allItems.OrderByDescending(i => i.OrderNo ?? ""),
            ("orderno", false) => allItems.OrderBy(i => i.OrderNo ?? ""),
            ("sequence", true) => allItems.OrderByDescending(i => i.Sequence),
            ("sequence", false) => allItems.OrderBy(i => i.Sequence),
            ("suppliername", true) => allItems.OrderByDescending(i => i.SupplierName ?? ""),
            ("suppliername", false) => allItems.OrderBy(i => i.SupplierName ?? ""),
            ("orderdate", true) => allItems.OrderByDescending(i => i.OrderDate),
            ("orderdate", false) => allItems.OrderBy(i => i.OrderDate),
            ("sourceworkorderno", true) => allItems.OrderByDescending(i => i.SourceWorkOrderNo ?? ""),
            ("sourceworkorderno", false) => allItems.OrderBy(i => i.SourceWorkOrderNo ?? ""),
            ("plantgrade", true) => allItems.OrderByDescending(i => i.PlantGrade ?? ""),
            ("plantgrade", false) => allItems.OrderBy(i => i.PlantGrade ?? ""),
            ("processspecification", true) => allItems.OrderByDescending(i => i.ProcessSpecification),
            ("processspecification", false) => allItems.OrderBy(i => i.ProcessSpecification),
            ("unitweight", true) => allItems.OrderByDescending(i => i.UnitWeight),
            ("unitweight", false) => allItems.OrderBy(i => i.UnitWeight),
            ("requiredquantity", true) => allItems.OrderByDescending(i => i.RequiredQuantity),
            ("requiredquantity", false) => allItems.OrderBy(i => i.RequiredQuantity),
            ("requiredweight", true) => allItems.OrderByDescending(i => i.RequiredWeight),
            ("requiredweight", false) => allItems.OrderBy(i => i.RequiredWeight),
            ("requiredarrivaldate", true) => allItems.OrderByDescending(i => i.RequiredArrivalDate),
            ("requiredarrivaldate", false) => allItems.OrderBy(i => i.RequiredArrivalDate),
            ("remark", true) => allItems.OrderByDescending(i => i.Remark ?? ""),
            ("remark", false) => allItems.OrderBy(i => i.Remark ?? ""),
            ("returndeadline", true) => allItems.OrderByDescending(i => i.ReturnDeadline),
            ("returndeadline", false) => allItems.OrderBy(i => i.ReturnDeadline),
            ("returnedquantity", true) => allItems.OrderByDescending(i => i.ReturnedQuantity),
            ("returnedquantity", false) => allItems.OrderBy(i => i.ReturnedQuantity),
            ("returnedweight", true) => allItems.OrderByDescending(i => i.ReturnedWeight),
            ("returnedweight", false) => allItems.OrderBy(i => i.ReturnedWeight),
            ("returnquantity", true) => allItems.OrderByDescending(i => i.ReturnQuantity),
            ("returnquantity", false) => allItems.OrderBy(i => i.ReturnQuantity),
            ("returnweight", true) => allItems.OrderByDescending(i => i.ReturnWeight),
            ("returnweight", false) => allItems.OrderBy(i => i.ReturnWeight),
            ("isforcecompleted", true) => allItems.OrderByDescending(i => i.IsForceCompleted),
            ("isforcecompleted", false) => allItems.OrderBy(i => i.IsForceCompleted),
            ("processstatus", true) => allItems.OrderByDescending(i => i.ProcessStatus),
            ("processstatus", false) => allItems.OrderBy(i => i.ProcessStatus),
            ("executionschedulestage", true) => allItems.OrderByDescending(i => i.ExecutionScheduleStage),
            ("executionschedulestage", false) => allItems.OrderBy(i => i.ExecutionScheduleStage),
            ("executionurgencylevel", true) => allItems.OrderByDescending(i => i.ExecutionUrgencyLevel ?? ""),
            ("executionurgencylevel", false) => allItems.OrderBy(i => i.ExecutionUrgencyLevel ?? ""),
            ("executionrawmateriallockremark", true) => allItems.OrderByDescending(i => i.ExecutionRawMaterialLockRemark ?? ""),
            ("executionrawmateriallockremark", false) => allItems.OrderBy(i => i.ExecutionRawMaterialLockRemark ?? ""),
            ("executiontheoreticalcutoffdate", true) => allItems.OrderByDescending(i => i.ExecutionTheoreticalCutoffDate),
            ("executiontheoreticalcutoffdate", false) => allItems.OrderBy(i => i.ExecutionTheoreticalCutoffDate),
            _ => allItems.OrderByDescending(i => i.Id)
        };

        var totalCount = sorted.Count();
        var items = sorted.Skip(query.Skip).Take(query.PageSize).ToList();

        return new PagedResult<SubcontractReturnItemListDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    // ========== 圆钢穿孔汇总（按子项聚合） ==========

    /// <summary>是否未完成子项（排除强制完成/已完成/超量回收；ProcessStatus 空视为未完成）</summary>
    private static bool IsUnfinished(SubcontractReturnItemListDto i)
        => !i.IsForceCompleted
           && i.ProcessStatus is not (SubcontractOrderStatus.Completed or SubcontractOrderStatus.OverReceived);

    /// <summary>
    /// 加载全部子项执行数据（含序号级退货量 + 工单实时关注），供三个穿孔汇总表复用。
    /// 退货量/工单关注口径与 GetReturnItemListAsync 完全一致。
    /// </summary>
    private async Task<List<SubcontractReturnItemListDto>> LoadPiercingSummaryItemsAsync()
    {
        var rawItems = await _context.SubcontractReturnItems
            .AsNoTracking()
            .Select(i => new
            {
                Id = i.Id,
                SubcontractOrderId = i.SubcontractOrderId,
                Sequence = i.Sequence,
                OrderNo = i.OrderNo ?? i.SubcontractOrder.OrderNo,
                SupplierName = i.SubcontractOrder.SupplierName,
                OrderDate = i.SubcontractOrder.OrderDate,
                SourceWorkOrderNo = i.SourceWorkOrderNo,
                PlantGrade = i.PlantGrade,
                ProcessSpecification = i.ProcessSpecification,
                RequiredWeight = i.RequiredWeight,
                ReturnedWeight = i.ReturnedWeight,
                IsForceCompleted = i.IsForceCompleted,
                ProcessStatus = i.ProcessStatus
            })
            .ToListAsync();

        var allItems = rawItems.Select(i => new SubcontractReturnItemListDto
        {
            Id = i.Id,
            SubcontractOrderId = i.SubcontractOrderId,
            Sequence = i.Sequence,
            OrderNo = i.OrderNo,
            SupplierName = i.SupplierName,
            OrderDate = i.OrderDate,
            SourceWorkOrderNo = i.SourceWorkOrderNo,
            PlantGrade = i.PlantGrade,
            ProcessSpecification = i.ProcessSpecification,
            RequiredWeight = i.RequiredWeight,
            ReturnedWeight = i.ReturnedWeight,
            IsForceCompleted = i.IsForceCompleted,
            ProcessStatus = EnumHelper.TryParse<SubcontractOrderStatus>(i.ProcessStatus)
        }).ToList();

        // 退货量（序号级）
        var orderNos = allItems.Where(x => !string.IsNullOrEmpty(x.OrderNo)).Select(x => x.OrderNo!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (orderNos.Count > 0)
        {
            var returnSummary = await BuildReturnSummaryAsync(orderNos);
            foreach (var item in allItems)
            {
                if (item.OrderNo != null && returnSummary.TryGetValue(item.OrderNo, out var rs)
                    && rs.BySequence.TryGetValue(item.Sequence, out var s))
                {
                    item.ReturnQuantity = s.Quantity;
                    item.ReturnWeight = s.Weight;
                }
            }
        }

        // 工单实时关注（按来源工单号关联工单执行状况读模型）
        var workOrderNos = allItems.Where(x => !string.IsNullOrEmpty(x.SourceWorkOrderNo)).Select(x => x.SourceWorkOrderNo!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (workOrderNos.Count > 0)
        {
            var execLookup = await _context.WorkOrderExecutionSummaries.AsNoTracking()
                .Where(e => workOrderNos.Contains(e.WorkOrderNo))
                .Select(e => new { e.WorkOrderNo, e.ScheduleStage, e.UrgencyLevel, e.RawMaterialLockRemark })
                .ToListAsync();
            var execMap = execLookup.ToDictionary(e => e.WorkOrderNo, e => e, StringComparer.OrdinalIgnoreCase);
            foreach (var item in allItems)
            {
                if (item.SourceWorkOrderNo != null && execMap.TryGetValue(item.SourceWorkOrderNo, out var exec))
                {
                    item.ExecutionScheduleStage = exec.ScheduleStage;
                    item.ExecutionUrgencyLevel = exec.UrgencyLevel;
                    item.ExecutionRawMaterialLockRemark = exec.RawMaterialLockRemark;
                }
            }
        }

        return allItems;
    }

    /// <summary>
    /// 按工单号关联工单执行状况读模型（工单关注/原锁执行/工单计划性），无记录返回默认
    /// </summary>
    private async Task<Dictionary<string, (int? Stage, string? Urgency, string? LockRemark)>> BuildExecutionMapAsync(List<string> workOrderNos)
    {
        var result = new Dictionary<string, (int?, string?, string?)>(StringComparer.OrdinalIgnoreCase);
        if (workOrderNos.Count == 0)
            return result;

        var rows = await _context.WorkOrderExecutionSummaries
            .AsNoTracking()
            .Where(e => workOrderNos.Contains(e.WorkOrderNo))
            .Select(e => new { e.WorkOrderNo, e.ScheduleStage, e.UrgencyLevel, e.RawMaterialLockRemark })
            .ToListAsync();

        foreach (var r in rows)
            result[r.WorkOrderNo] = (r.ScheduleStage, r.UrgencyLevel, r.RawMaterialLockRemark);

        return result;
    }

    /// <summary>
    /// 圆钢待穿孔（明细，行=工单）：数据源=圆棒穿孔计划需求 − 已下委外量（子项需求重量按工单聚合）；
    /// 缺少量=Max(0, 需求重量-已下委外量)，列结构对齐「荒管待购」（仅缺少量，规格只留穿孔规格）。
    /// 尚未决定穿孔单位，故无委外单号/序号/委外单位列；含工单实时关注
    /// </summary>
    public async Task<List<SubcontractPiercingPendingDto>> GetPiercingPendingAsync()
    {
        // 1. 圆棒穿孔计划（需要穿孔的圆钢需求）
        var planRows = await _context.RoundBarPiercingPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0)
            .Select(p => new { p.WorkOrderId, p.PlantGrade, p.PiercingSpec, p.RequiredWeight })
            .ToListAsync();
        if (planRows.Count == 0)
            return new List<SubcontractPiercingPendingDto>();

        // 2. 工单号映射
        var woIds = planRows.Select(p => p.WorkOrderId).Distinct().ToList();
        var workOrders = await _context.WorkOrders.AsNoTracking()
            .Where(w => woIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.WorkOrderNo);
        var allWorkOrderNos = workOrders.Values.ToList();

        // 3. 已下委外量 = 子项需求重量按工单聚合（发出量口径，与 GetPiercingProcurementStatusAsync 一致）
        var dispatchedWeights = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (allWorkOrderNos.Count > 0)
        {
            var subData = await _context.SubcontractReturnItems
                .AsNoTracking()
                .Where(r => r.SourceWorkOrderNo != null && allWorkOrderNos.Contains(r.SourceWorkOrderNo))
                .GroupBy(r => r.SourceWorkOrderNo!)
                .Select(g => new { g.Key, Weight = g.Sum(r => r.RequiredWeight ?? 0) })
                .ToListAsync();
            dispatchedWeights = subData.ToDictionary(x => x.Key, x => x.Weight, StringComparer.OrdinalIgnoreCase);
        }

        // 4. 工单实时关注
        var execMap = await BuildExecutionMapAsync(allWorkOrderNos);

        // 5. 按工单号分组聚合
        return planRows
            .GroupBy(x => x.WorkOrderId)
            .Select(g =>
            {
                var workOrderNo = workOrders.GetValueOrDefault(g.Key, "");
                var dispatched = dispatchedWeights.GetValueOrDefault(workOrderNo, 0);
                var (stage, urgency, lockRemark) = execMap.GetValueOrDefault(workOrderNo);
                return new SubcontractPiercingPendingDto
                {
                    WorkOrderNo = workOrderNo,
                    PlantGrade = string.Join(",", g.Select(x => x.PlantGrade).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)),
                    PiercingSpec = string.Join(",", g.Select(x => x.PiercingSpec).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)),
                    MissingWeight = Math.Max(0, g.Sum(x => x.RequiredWeight) - dispatched),
                    ExecutionScheduleStage = stage,
                    ExecutionUrgencyLevel = urgency,
                    ExecutionRawMaterialLockRemark = lockRemark
                };
            })
            .Where(x => x.MissingWeight > 0 && !string.IsNullOrEmpty(x.WorkOrderNo))
            .OrderBy(x => x.WorkOrderNo)
            .ToList();
    }

    /// <summary>
    /// 圆钢在穿孔（二维：委外单位×加工规格）：在穿孔量=Max(0, 需求-净回收)，排除已完成/强制完成；
    /// 不含急量；含合计行
    /// </summary>
    public async Task<SubcontractPiercingInProgressResultDto> GetPiercingInProgressAsync()
    {
        var result = new SubcontractPiercingInProgressResultDto();
        var items = await LoadPiercingSummaryItemsAsync();

        var unfinished = items
            .Where(IsUnfinished)
            .Select(i =>
            {
                var pending = Math.Max(0, (i.RequiredWeight ?? 0m) - Math.Max(0, i.ReturnedWeight - i.ReturnWeight));
                return new
                {
                    Supplier = string.IsNullOrWhiteSpace(i.SupplierName) ? "未填写单位" : i.SupplierName.Trim(),
                    Spec = string.IsNullOrWhiteSpace(i.ProcessSpecification) ? "" : i.ProcessSpecification.Trim(),
                    Pending = pending
                };
            })
            .Where(x => x.Pending > 0)
            .ToList();
        if (unfinished.Count == 0)
        {
            // 无数据时仅保留全 0 合计行，前端仍可渲染表结构
            result.Rows.Add(new SubcontractPiercingInProgressRowDto { SupplierName = "合计" });
            return result;
        }

        result.Specifications = unfinished.Select(x => x.Spec)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rowMap = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase);
        var totalMap = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var x in unfinished)
        {
            if (!rowMap.TryGetValue(x.Supplier, out var cells))
            {
                cells = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                rowMap[x.Supplier] = cells;
            }
            cells[x.Spec] = cells.GetValueOrDefault(x.Spec) + x.Pending;
            totalMap[x.Supplier] = totalMap.GetValueOrDefault(x.Supplier) + x.Pending;
        }

        foreach (var kvp in rowMap.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var cells = new Dictionary<string, SubcontractPiercingInProgressCellDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in result.Specifications)
                cells[s] = new SubcontractPiercingInProgressCellDto { TotalWeight = kvp.Value.GetValueOrDefault(s) };
            result.Rows.Add(new SubcontractPiercingInProgressRowDto
            {
                SupplierName = kvp.Key,
                Cells = cells,
                Total = new SubcontractPiercingInProgressCellDto { TotalWeight = totalMap.GetValueOrDefault(kvp.Key) }
            });
        }

        // 合计行
        var specTotals = result.Specifications.ToDictionary(
            s => s,
            s => new SubcontractPiercingInProgressCellDto
            {
                TotalWeight = result.Rows.Sum(r => r.Cells.GetValueOrDefault(s)?.TotalWeight ?? 0)
            },
            StringComparer.OrdinalIgnoreCase);
        result.Rows.Add(new SubcontractPiercingInProgressRowDto
        {
            SupplierName = "合计",
            Cells = specTotals,
            Total = new SubcontractPiercingInProgressCellDto { TotalWeight = result.Rows.Sum(r => r.Total.TotalWeight) }
        });

        return result;
    }

    /// <summary>
    /// 圆钢月度穿孔数据（二维：委外单位×1~12月）：发=该月下单的需求重量，回=净回收重量；
    /// 现在穿=未完成子项的在穿孔量（不分加工规格）；含合计行。发/回仅统计本年下单子项（同采购月度口径）。
    /// </summary>
    public async Task<SubcontractPiercingMonthlyResultDto> GetPiercingMonthlyAsync()
    {
        var result = new SubcontractPiercingMonthlyResultDto();
        var year = DateTime.Today.Year;
        var labels = Enumerable.Range(1, 12).Select(m => $"{year}-{m:00}").ToList();
        result.MonthLabels = labels;

        var items = await LoadPiercingSummaryItemsAsync();
        if (items.Count == 0)
        {
            // 无子项时仅保留全 0 合计行，前端仍可渲染表结构
            result.Rows.Add(new SubcontractPiercingMonthlyRowDto
            {
                SupplierName = "合计",
                Months = labels.Select(_ => new SubcontractPiercingMonthlyValueDto()).ToList()
            });
            return result;
        }

        var rowMap = new Dictionary<string, SubcontractPiercingMonthlyRowDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in items)
        {
            // 发/回仅统计本年下单的子项
            if (i.OrderDate.Year != year) continue;

            var supplier = string.IsNullOrWhiteSpace(i.SupplierName) ? "未填写单位" : i.SupplierName.Trim();
            if (!rowMap.TryGetValue(supplier, out var row))
            {
                row = new SubcontractPiercingMonthlyRowDto
                {
                    SupplierName = supplier,
                    Months = labels.Select(_ => new SubcontractPiercingMonthlyValueDto()).ToList()
                };
                rowMap[supplier] = row;
            }

            var monthIdx = i.OrderDate.Month - 1;
            var req = i.RequiredWeight ?? 0m;
            var net = Math.Max(0, i.ReturnedWeight - i.ReturnWeight);
            row.Months[monthIdx].SendWeight += req;
            row.Months[monthIdx].RecoverWeight += net;
            row.Total.SendWeight += req;
            row.Total.RecoverWeight += net;

            // 现在穿 = 未完成子项的在穿孔量
            if (IsUnfinished(i))
            {
                var pending = Math.Max(0, req - net);
                if (pending > 0) row.NowPiercing += pending;
            }
        }

        result.Rows = rowMap.Values.OrderBy(x => x.SupplierName, StringComparer.OrdinalIgnoreCase).ToList();

        // 合计行
        var totalRow = new SubcontractPiercingMonthlyRowDto
        {
            SupplierName = "合计",
            Months = labels.Select(_ => new SubcontractPiercingMonthlyValueDto()).ToList()
        };
        foreach (var r in result.Rows)
        {
            for (var i = 0; i < labels.Count; i++)
            {
                totalRow.Months[i].SendWeight += r.Months[i].SendWeight;
                totalRow.Months[i].RecoverWeight += r.Months[i].RecoverWeight;
            }
            totalRow.Total.SendWeight += r.Total.SendWeight;
            totalRow.Total.RecoverWeight += r.Total.RecoverWeight;
            totalRow.NowPiercing += r.NowPiercing;
        }
        result.Rows.Add(totalRow);

        return result;
    }

    private static string FormatFilterValue(object val)
    {
        return val switch
        {
            DateTime dt => dt.ToString("yyyy-MM-dd"),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd"),
            _ => val.ToString() ?? ""
        };
    }

    public async Task<Dictionary<string, List<string>>> GetReturnItemFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("SubcontractOrderService:ReturnItemFilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var all = await _context.SubcontractReturnItems
                .AsNoTracking()
                .Select(i => new
                {
                    i.OrderNo,
                    i.SourceWorkOrderNo,
                    i.PlantGrade,
                    i.ProcessSpecification,
                    i.ProcessStatus,
                    SupplierName = i.SubcontractOrder.SupplierName,
                    ParentOrderNo = i.SubcontractOrder.OrderNo,
                    OrderDate = i.SubcontractOrder.OrderDate,
                    ReturnDeadline = i.SubcontractOrder.ReturnDeadline,
                    i.Remark,
                    i.IsForceCompleted
                })
                .ToListAsync();

            // 委外单号集合（子项 OrderNo 优先，回退主表 OrderNo）
            var subOrderNos = all
                .Select(x => x.OrderNo ?? x.ParentOrderNo)
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 截止回收日下拉：按委外单号反查仓库批实际入库日期 InboundDate
            var returnDeadlineDates = subOrderNos.Count > 0
                ? await _context.InventoryBatches.AsNoTracking()
                    .Where(b => b.SourceOrderNo != null && subOrderNos.Contains(b.SourceOrderNo))
                    .Select(b => b.InboundDate.ToString("yyyy-MM-dd"))
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync()
                : new List<string>();

            // 工单实时关注筛选上下文：按来源工单号关联工单执行状况读模型（无记录不参与 DISTINCT）
            var execWoNos = all.Where(x => x.SourceWorkOrderNo != null)
                .Select(x => x.SourceWorkOrderNo!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var execMap = new Dictionary<string, (string? UrgencyLevel, string? RawMaterialLockRemark, DateTime? TheoreticalCutoffDate)>(StringComparer.OrdinalIgnoreCase);
            if (execWoNos.Count > 0)
            {
                var execRows = await _context.WorkOrderExecutionSummaries.AsNoTracking()
                    .Where(e => execWoNos.Contains(e.WorkOrderNo))
                    .Select(e => new { e.WorkOrderNo, e.UrgencyLevel, e.RawMaterialLockRemark, e.TheoreticalCutoffDate })
                    .ToListAsync();
                foreach (var r in execRows)
                    execMap[r.WorkOrderNo] = (r.UrgencyLevel, r.RawMaterialLockRemark, r.TheoreticalCutoffDate);
            }

            // 空值（无工单号/读模型无记录）以哨兵 "__EXCEL_FILTER_NULL__" 输出，供筛选下拉「空值」选项体现
            var hasNullExec = all.Any(x => string.IsNullOrEmpty(x.SourceWorkOrderNo) || !execMap.ContainsKey(x.SourceWorkOrderNo!));
            var execUrgencyLevels = execMap.Values.Select(x => x.UrgencyLevel ?? FilterNull).Distinct().ToList();
            var execLockRemarks = execMap.Values.Select(x => x.RawMaterialLockRemark ?? FilterNull).Distinct().ToList();
            var execCutoffDates = execMap.Values
                .Select(x => x.TheoreticalCutoffDate != null ? x.TheoreticalCutoffDate.Value.ToString("yyyy-MM-dd") : FilterNull)
                .Distinct().ToList();
            if (hasNullExec)
            {
                if (!execUrgencyLevels.Contains(FilterNull)) execUrgencyLevels.Add(FilterNull);
                if (!execLockRemarks.Contains(FilterNull)) execLockRemarks.Add(FilterNull);
                if (!execCutoffDates.Contains(FilterNull)) execCutoffDates.Add(FilterNull);
            }

            return new Dictionary<string, List<string>>
            {
                ["OrderNo"] = all.Where(x => x.OrderNo != null || x.ParentOrderNo != null)
                    .Select(x => x.OrderNo ?? x.ParentOrderNo!).Distinct().OrderBy(x => x).ToList(),
                ["SourceWorkOrderNo"] = all.Where(x => x.SourceWorkOrderNo != null).Select(x => x.SourceWorkOrderNo!).Distinct().OrderBy(x => x).ToList(),
                ["SupplierName"] = all.Where(x => x.SupplierName != null).Select(x => x.SupplierName!).Distinct().OrderBy(x => x).ToList(),
                ["PlantGrade"] = all.Where(x => x.PlantGrade != null).Select(x => x.PlantGrade!).Distinct().OrderBy(x => x).ToList(),
                ["ProcessSpecification"] = all.Select(x => x.ProcessSpecification).Distinct().OrderBy(x => x).ToList(),
                ["ProcessStatus"] = all.Select(x => x.ProcessStatus).Distinct().OrderBy(x => x).ToList(),
                ["OrderDate"] = all.Select(x => x.OrderDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["RequiredArrivalDate"] = all.Where(x => x.ReturnDeadline != null)
                    .Select(x => x.ReturnDeadline!.Value.ToString("yyyy-MM-dd"))
                    .Distinct().OrderBy(x => x).ToList(),
                ["ReturnDeadline"] = returnDeadlineDates,
                ["Remark"] = all.Where(x => x.Remark != null).Select(x => x.Remark!).Distinct().OrderBy(x => x).ToList(),
                ["IsForceCompleted"] = all.Select(x => x.IsForceCompleted ? "True" : "False").Distinct().OrderBy(x => x).ToList(),
                ["ExecutionUrgencyLevel"] = execUrgencyLevels.OrderBy(x => x == FilterNull ? 0 : 1).ThenBy(x => x).ToList(),
                ["ExecutionRawMaterialLockRemark"] = execLockRemarks.OrderBy(x => x == FilterNull ? 0 : 1).ThenBy(x => x).ToList(),
                ["ExecutionTheoreticalCutoffDate"] = execCutoffDates.OrderBy(x => x == FilterNull ? 0 : 1).ThenBy(x => x).ToList(),
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    /// <summary>
    /// 按「委外单号 × 序号」汇总其退货量：退货出库 ReturnSourceBatchNo（原仓库批批次号）→ 反查 InventoryBatch.BatchNo → 其 SourceOrderNo == 委外单号。
    /// 返回（委外单号 →（序号 → 退货量/退货重, 截止回收日））；截止回收日=该委外单号回收入库仓库批 InboundDate 的最大值（实际收回入库日期）。
    /// 委外单号级退货量由 BySequence 各序号求和派生（详情表头/列表页/主表状态判定使用）。
    /// 聚合复用 SubcontractHelper.AggregateReturnsBySequence（与 InventorySyncService 退货口径一致）。
    /// </summary>
    private async Task<Dictionary<string, (Dictionary<int, (int Quantity, decimal Weight)> BySequence, DateTime? LastDate)>> BuildReturnSummaryAsync(IReadOnlyCollection<string> orderNos)
    {
        var result = new Dictionary<string, (Dictionary<int, (int Quantity, decimal Weight)> BySequence, DateTime? LastDate)>(StringComparer.OrdinalIgnoreCase);
        if (orderNos.Count == 0) return result;

        foreach (var no in orderNos.Distinct(StringComparer.OrdinalIgnoreCase))
            result[no] = (new Dictionary<int, (int, decimal)>(), null);

        // 按委外单号查其回收入库的仓库批
        var batches = await _context.InventoryBatches.AsNoTracking()
            .Where(b => b.SourceOrderNo != null && orderNos.Contains(b.SourceOrderNo))
            .Select(b => new { b.BatchNo, b.SourceOrderNo, b.SourceOrderSequence, b.InboundDate })
            .ToListAsync();

        // 截止回收日：按委外单号取 InboundDate 最大值（无回收入库 → 保持 null）
        var lastDateByOrderNo = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in batches.Where(b => !string.IsNullOrEmpty(b.SourceOrderNo)))
        {
            if (lastDateByOrderNo.TryGetValue(b.SourceOrderNo!, out var cur) && cur >= b.InboundDate) continue;
            lastDateByOrderNo[b.SourceOrderNo!] = b.InboundDate;
        }
        foreach (var no in result.Keys.ToList())
        {
            if (lastDateByOrderNo.TryGetValue(no, out var d))
            {
                var (bySeq, _) = result[no];
                result[no] = (bySeq, d);
            }
        }

        var batchNos = batches.Where(b => !string.IsNullOrEmpty(b.BatchNo)).Select(b => b.BatchNo!).ToList();
        if (batchNos.Count == 0) return result;

        var outbounds = new List<OutboundRecord>();
        foreach (var chunk in batchNos.Chunk(1000))
        {
            outbounds.AddRange(await _context.OutboundRecords.AsNoTracking()
                .Where(o => o.OutboundType == OutboundType.ReturnOut
                         && o.ReturnSourceBatchNo != null
                         && chunk.Contains(o.ReturnSourceBatchNo))
                .ToListAsync());
        }

        var bySeqMap = SubcontractHelper.AggregateReturnsBySequence(
            outbounds,
            batches.Select(b => (b.BatchNo, b.SourceOrderNo, b.SourceOrderSequence)));

        foreach (var no in result.Keys.ToList())
        {
            if (bySeqMap.TryGetValue(no, out var bySeq))
            {
                var (_, d) = result[no];
                result[no] = (bySeq, d);
            }
        }

        return result;
    }

    /// <summary>
    /// 委外单号级退货重量（主表净回收状态判定用，= 各序号退货重量之和）。
    /// </summary>
    private async Task<decimal> GetOrderReturnWeightAsync(string orderNo)
    {
        var summary = await BuildReturnSummaryAsync(new[] { orderNo });
        return summary.TryGetValue(orderNo, out var rs) ? rs.BySequence.Values.Sum(x => x.Weight) : 0m;
    }

    public async Task<byte[]> PrintReturnItemSelectedAsync(int[] ids, List<PrintColumnDef>? columns)
    {
        var rawItems = await _context.SubcontractReturnItems
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .Select(i => new
            {
                Id = i.Id,
                SubcontractOrderId = i.SubcontractOrderId,
                Sequence = i.Sequence,
                OrderNo = i.OrderNo ?? i.SubcontractOrder.OrderNo,
                SupplierName = i.SubcontractOrder.SupplierName,
                OrderDate = i.SubcontractOrder.OrderDate,
                SourceWorkOrderNo = i.SourceWorkOrderNo,
                PlantGrade = i.PlantGrade,
                ProcessSpecification = i.ProcessSpecification,
                UnitWeight = i.UnitWeight,
                RequiredQuantity = i.RequiredQuantity,
                RequiredWeight = i.RequiredWeight,
                ReturnDeadline = i.SubcontractOrder.ReturnDeadline,
                Remark = i.Remark,
                ReturnedQuantity = i.ReturnedQuantity,
                ReturnedWeight = i.ReturnedWeight,
                IsForceCompleted = i.IsForceCompleted,
                ProcessStatus = i.ProcessStatus
            })
            .ToListAsync();

        var items = rawItems.Select(i => new SubcontractReturnItemListDto
        {
            Id = i.Id,
            SubcontractOrderId = i.SubcontractOrderId,
            Sequence = i.Sequence,
            OrderNo = i.OrderNo,
            SupplierName = i.SupplierName,
            OrderDate = i.OrderDate,
            SourceWorkOrderNo = i.SourceWorkOrderNo,
            PlantGrade = i.PlantGrade,
            ProcessSpecification = i.ProcessSpecification,
            UnitWeight = i.UnitWeight,
            RequiredQuantity = i.RequiredQuantity,
            RequiredWeight = i.RequiredWeight,
            RequiredArrivalDate = i.ReturnDeadline,
            Remark = i.Remark,
            ReturnedQuantity = i.ReturnedQuantity,
            ReturnedWeight = i.ReturnedWeight,
            IsForceCompleted = i.IsForceCompleted,
            ProcessStatus = EnumHelper.TryParse<SubcontractOrderStatus>(i.ProcessStatus)
        }).ToList();

        // 退货量（序号级）+ 截止回收日补充（同 GetReturnItemListAsync）
        var orderNos = items.Select(x => x.OrderNo)
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (orderNos.Count > 0)
        {
            var returnSummary = await BuildReturnSummaryAsync(orderNos);
            foreach (var item in items)
            {
                if (item.OrderNo != null && returnSummary.TryGetValue(item.OrderNo, out var rs))
                {
                    if (rs.BySequence.TryGetValue(item.Sequence, out var s))
                    {
                        item.ReturnQuantity = s.Quantity;
                        item.ReturnWeight = s.Weight;
                    }
                    item.ReturnDeadline = rs.LastDate;
                }
            }
        }

        var resolvers = new Dictionary<string, Func<object?, string>>
        {
            ["ProcessStatus"] = v => v is SubcontractOrderStatus ps
                ? EnumHelper.GetDisplayName(ps)
                : "-"
        };

        return TablePrintHelper.GeneratePdf("子项查询", items, columns ?? new List<PrintColumnDef>(), resolvers);
    }

    // ========== 打印 ==========

    public async Task<byte[]> PrintOrderAsync(int id)
    {
        var dto = await GetByIdAsync(id);
        return SubcontractOrderPrintHelper.GeneratePdf(dto);
    }

    public async Task<byte[]> PrintOrderBatchAsync(int[] ids)
    {
        var orders = await GetByIdsAsync(ids);
        return SubcontractOrderPrintHelper.GenerateBatchPdf(orders);
    }

    public async Task<List<SubcontractOrderDto>> GetByIdsAsync(int[] ids)
    {
        var entities = await _context.SubcontractOrders
            .AsNoTracking()
            .Include(s => s.ReturnItems.OrderBy(r => r.Sequence))
            .Where(s => ids.Contains(s.Id))
            .ToListAsync();

        var woNos = entities.SelectMany(e => e.ReturnItems)
            .Where(r => !string.IsNullOrEmpty(r.SourceWorkOrderNo))
            .Select(r => r.SourceWorkOrderNo!)
            .Distinct()
            .ToList();
        var workOrders = new Dictionary<string, WoEntity>();
        if (woNos.Count > 0)
        {
            workOrders = await _context.WorkOrders
                .AsNoTracking()
                .Where(w => woNos.Contains(w.WorkOrderNo))
                .ToDictionaryAsync(w => w.WorkOrderNo, w => w);
        }

        // 退货量补充：委外单号级（各序号求和）+ 序号级
        var orderNos = entities.Select(e => e.OrderNo).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var returnSummary = orderNos.Count > 0 ? await BuildReturnSummaryAsync(orderNos) : new Dictionary<string, (Dictionary<int, (int Quantity, decimal Weight)> BySequence, DateTime? LastDate)>(StringComparer.OrdinalIgnoreCase);

        return entities.Select(e =>
        {
            var dto = ToDto(e);

            var returnBySequence = returnSummary.TryGetValue(e.OrderNo, out var rs) ? rs.BySequence : new Dictionary<int, (int Quantity, decimal Weight)>();
            dto.ReturnQuantity = returnBySequence.Values.Sum(x => x.Quantity);
            dto.ReturnWeight = returnBySequence.Values.Sum(x => x.Weight);

            dto.ReturnItems = e.ReturnItems.Select(r =>
            {
                var itemDto = new SubcontractReturnItemDto
                {
                    Id = r.Id,
                    SubcontractOrderId = r.SubcontractOrderId,
                    Sequence = r.Sequence,
                    MaterialCategory = !string.IsNullOrEmpty(r.MaterialCategory) && Enum.TryParse<MaterialType>(r.MaterialCategory, out var rc) ? rc : default,
                    PlantGrade = r.PlantGrade,
                    ProcessSpecification = r.ProcessSpecification,
                    UnitWeight = r.UnitWeight,
                    RequiredQuantity = r.RequiredQuantity,
                    RequiredWeight = r.RequiredWeight,
                    ProcessStatusRemark = r.ProcessStatusRemark,
                    Remark = r.Remark,
                    ProcessUnitPrice = r.ProcessUnitPrice,
                    ProcessTotalAmount = r.ProcessTotalAmount,
                    SourceWorkOrderNo = r.SourceWorkOrderNo,
                    ReturnedQuantity = r.ReturnedQuantity,
                    ReturnedWeight = r.ReturnedWeight,
                    ProcessStatus = Enum.TryParse<SubcontractOrderStatus>(r.ProcessStatus, out var ps) ? ps : default,
                    IsForceCompleted = r.IsForceCompleted
                };
                if (returnBySequence.TryGetValue(r.Sequence, out var ret))
                {
                    itemDto.ReturnQuantity = ret.Quantity;
                    itemDto.ReturnWeight = ret.Weight;
                }
                if (r.SourceWorkOrderNo != null && workOrders.TryGetValue(r.SourceWorkOrderNo, out var wo))
                    FillWorkOrderFields(itemDto, wo);
                return itemDto;
            }).ToList();

            return dto;
        }).ToList();
    }

    public async Task<byte[]> PrintOrderAllAsync(string? keyword, string? sortBy = null, bool isDescending = false, DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        var query = new SubcontractQueryParams
        {
            PageIndex = 1,
            PageSize = 10000,
            Keyword = keyword,
            SortBy = sortBy ?? "CreatedTime",
            IsDescending = isDescending,
            DateFrom = dateFrom,
            DateTo = dateTo
        };
        var result = await GetPagedAsync(query);
        return SubcontractOrderPrintHelper.GenerateBatchPdf(result.Items);
    }

    // ========== 私有方法 ==========

    private async Task RecalcSubcontractStatusAsync(SubcontractOrder order, decimal? knownReturnWeight = null)
    {
        var ratio = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteRatio", 0.965m);
        var deviation = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteDeviation", 200m);

        // 净回收 = 总回收 - 退货量（与采购订单「按回收量-退货量计算真正的回收量」口径一致）
        var returnWeight = knownReturnWeight ?? await GetOrderReturnWeightAsync(order.OrderNo);
        var netRecover = order.InWeight.HasValue ? Math.Max(0m, order.InWeight.Value - returnWeight) : 0m;

        if (netRecover <= 0m)
            order.Status = SubcontractOrderStatus.Sent;
        else if (PurchaseOrderService.IsThresholdMet(netRecover, order.OutWeight, ratio, deviation))
            order.Status = SubcontractOrderStatus.Completed;
        else
            order.Status = SubcontractOrderStatus.PartialReturned;
    }

    private async Task<string> GenerateOrderNoAsync()
    {
        var today = DateTime.Now.ToString("yyMMdd");
        var prefix = $"WW{today}";
        var existingNos = await _context.SubcontractOrders
            .Where(s => s.OrderNo.StartsWith(prefix) && s.OrderNo.Length == prefix.Length + 3)
            .Select(s => s.OrderNo)
            .ToListAsync();

        int maxSeq = 0;
        foreach (var no in existingNos)
        {
            if (int.TryParse(no[^3..], out var s) && s > maxSeq)
                maxSeq = s;
        }

        return $"{prefix}{maxSeq + 1:D3}";
    }

    private static SubcontractOrderDto ToDto(SubcontractOrder entity) => new()
    {
        Id = entity.Id,
        OrderNo = entity.OrderNo,
        SupplierId = entity.SupplierId,
        SupplierName = entity.SupplierName ?? "",
        OrderDate = entity.OrderDate,
        Status = entity.Status,
        IsForceCompleted = entity.IsForceCompleted,
        FurnaceNumber = entity.FurnaceNumber,
        ProcessType = entity.ProcessType ?? "Piercing",
        OutMaterialCategory = !string.IsNullOrEmpty(entity.OutMaterialCategory) && Enum.TryParse<MaterialType>(entity.OutMaterialCategory, out var category) ? category : default,
        OutPlantGrade = entity.OutPlantGrade,
        OutSpecification = entity.OutSpecification,
        OutQuantity = entity.OutQuantity,
        OutWeight = entity.OutWeight,
        ReturnDeadline = entity.ReturnDeadline,
        InQuantity = entity.InQuantity,
        InWeight = entity.InWeight,
        Remark = entity.Remark,
        CreatedBy = entity.CreatedBy,
        CreatedTime = entity.CreatedTime,
        UpdatedBy = entity.UpdatedBy,
        UpdatedTime = entity.UpdatedTime
    };

    private static void FillWorkOrderFields(SubcontractOrderDto dto, WoEntity wo)
    {
        dto.WoSalesOrderNo = wo.SalesOrderNo;
        dto.WoProductionMainNo = wo.ProductionMainNo;
        dto.WoProductionSubNo = wo.ProductionSubNo;
        dto.WoSignDate = wo.SignDate;
        dto.WoSalesman = wo.Salesman;
        dto.WoEndCustomer = wo.EndCustomer;
        dto.WoDeliveryDate = wo.DeliveryDate;
        dto.WoDelayPenalty = wo.DelayPenalty;
        dto.WoSettlementMethod = wo.SettlementMethod;
        dto.WoPlantGrade = wo.PlantGrade;
        dto.WoSpecification = wo.Specification;
        dto.WoLengthStatus = wo.LengthStatus;
        dto.WoMaxLength = wo.MaxLength;
        dto.WoTotalQuantity = wo.TotalQuantity;
        dto.WoTotalWeight = wo.TotalWeight;
        dto.WoDeliveryState = wo.DeliveryState;
        dto.WoTotalItemCount = wo.TotalItemCount;
    }

    private static void FillWorkOrderFields(SubcontractReturnItemDto dto, WoEntity wo)
    {
        dto.WoSalesOrderNo = wo.SalesOrderNo;
        dto.WoProductionMainNo = wo.ProductionMainNo;
        dto.WoProductionSubNo = wo.ProductionSubNo;
        dto.WoSignDate = wo.SignDate;
        dto.WoSalesman = wo.Salesman;
        dto.WoEndCustomer = wo.EndCustomer;
        dto.WoDeliveryDate = wo.DeliveryDate;
        dto.WoDelayPenalty = wo.DelayPenalty;
        dto.WoSettlementMethod = wo.SettlementMethod;
        dto.WoPlantGrade = wo.PlantGrade;
        dto.WoSpecification = wo.Specification;
        dto.WoLengthStatus = wo.LengthStatus;
        dto.WoMaxLength = wo.MaxLength;
        dto.WoTotalQuantity = wo.TotalQuantity;
        dto.WoTotalWeight = wo.TotalWeight;
        dto.WoDeliveryState = wo.DeliveryState;
        dto.WoTotalItemCount = wo.TotalItemCount;
    }
}
