using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Auth;
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

        return entityList.Select(ToDto).ToList();
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

        foreach (var order in orders)
        {
            var orderBatches = batches.Where(b => b.SourceOrderNo == order.OrderNo).ToList();

            order.InQuantity = orderBatches.Sum(b => b.InitialQuantity);
            order.InWeight = orderBatches.Sum(b => b.InitialWeight);

            // 同步每个 ReturnItem 的回收数据
            foreach (var item in order.ReturnItems)
            {
                SubcontractHelper.SyncReturnItemFromBatches(item, orderBatches);
            }

            // 主表强制完成 → 子表全部强制完成
            if (order.IsForceCompleted)
                ForceCompleteAllReturnItems(order);
            else
                await RecalcSubcontractStatusAsync(order);
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

        // 同步每个 ReturnItem 的回收数据
        foreach (var item in order.ReturnItems)
        {
            SubcontractHelper.SyncReturnItemFromBatches(item, batches);
        }

        // 主表强制完成 → 子表全部强制完成
        if (order.IsForceCompleted)
            ForceCompleteAllReturnItems(order);
        else if (!order.IsForceCompleted)
            await RecalcSubcontractStatusAsync(order);

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
            await RecalcSubcontractStatusAsync(entity);
            // 取消级联：每个子表按实际回收数据重新计算
            foreach (var item in entity.ReturnItems)
            {
                item.IsForceCompleted = false;
                SubcontractHelper.RecalcReturnItemStatus(item);
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
        var allItems = await queryable
            .Select(i => new SubcontractReturnItemListDto
            {
                Id = i.Id,
                SubcontractOrderId = i.SubcontractOrderId,
                OrderNo = i.OrderNo ?? i.SubcontractOrder.OrderNo,
                SupplierName = i.SubcontractOrder.SupplierName,
                SourceWorkOrderNo = i.SourceWorkOrderNo,
                PlantGrade = i.PlantGrade,
                ProcessSpecification = i.ProcessSpecification,
                UnitWeight = i.UnitWeight,
                RequiredQuantity = i.RequiredQuantity,
                RequiredWeight = i.RequiredWeight,
                ReturnDeadline = i.SubcontractOrder.ReturnDeadline,
                ReturnedQuantity = i.ReturnedQuantity,
                ReturnedWeight = i.ReturnedWeight,
                ProcessStatus = i.ProcessStatus
            })
            .ToListAsync();

        // 内存筛选 — 支持所有 DTO 属性（包括跨表字段如 OrderNo、ReturnDeadline）
        if (query.Filters?.Count > 0)
        {
            var dtoType = typeof(SubcontractReturnItemListDto);
            foreach (var filter in query.Filters)
            {
                var prop = dtoType.GetProperty(filter.Field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null) continue;

                var op = filter.Operator ?? "contains";
                if (op == "in" && filter.Values?.Count > 0)
                {
                    var filterValues = filter.Values;
                    allItems = allItems.Where(item =>
                    {
                        var val = prop.GetValue(item);
                        if (val == null) return false;
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
            ("suppliername", true) => allItems.OrderByDescending(i => i.SupplierName ?? ""),
            ("suppliername", false) => allItems.OrderBy(i => i.SupplierName ?? ""),
            ("sourceworkorderno", true) => allItems.OrderByDescending(i => i.SourceWorkOrderNo ?? ""),
            ("sourceworkorderno", false) => allItems.OrderBy(i => i.SourceWorkOrderNo ?? ""),
            ("plantgrade", true) => allItems.OrderByDescending(i => i.PlantGrade ?? ""),
            ("plantgrade", false) => allItems.OrderBy(i => i.PlantGrade ?? ""),
            ("processspecification", true) => allItems.OrderByDescending(i => i.ProcessSpecification),
            ("processspecification", false) => allItems.OrderBy(i => i.ProcessSpecification),
            ("requiredquantity", true) => allItems.OrderByDescending(i => i.RequiredQuantity),
            ("requiredquantity", false) => allItems.OrderBy(i => i.RequiredQuantity),
            ("requiredweight", true) => allItems.OrderByDescending(i => i.RequiredWeight),
            ("requiredweight", false) => allItems.OrderBy(i => i.RequiredWeight),
            ("returndeadline", true) => allItems.OrderByDescending(i => i.ReturnDeadline),
            ("returndeadline", false) => allItems.OrderBy(i => i.ReturnDeadline),
            ("returnedquantity", true) => allItems.OrderByDescending(i => i.ReturnedQuantity),
            ("returnedquantity", false) => allItems.OrderBy(i => i.ReturnedQuantity),
            ("returnedweight", true) => allItems.OrderByDescending(i => i.ReturnedWeight),
            ("returnedweight", false) => allItems.OrderBy(i => i.ReturnedWeight),
            ("processstatus", true) => allItems.OrderByDescending(i => i.ProcessStatus),
            ("processstatus", false) => allItems.OrderBy(i => i.ProcessStatus),
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
                    ReturnDeadline = i.SubcontractOrder.ReturnDeadline
                })
                .ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["OrderNo"] = all.Where(x => x.OrderNo != null || x.ParentOrderNo != null)
                    .Select(x => x.OrderNo ?? x.ParentOrderNo!).Distinct().OrderBy(x => x).ToList(),
                ["SourceWorkOrderNo"] = all.Where(x => x.SourceWorkOrderNo != null).Select(x => x.SourceWorkOrderNo!).Distinct().OrderBy(x => x).ToList(),
                ["SupplierName"] = all.Where(x => x.SupplierName != null).Select(x => x.SupplierName!).Distinct().OrderBy(x => x).ToList(),
                ["PlantGrade"] = all.Where(x => x.PlantGrade != null).Select(x => x.PlantGrade!).Distinct().OrderBy(x => x).ToList(),
                ["ProcessSpecification"] = all.Select(x => x.ProcessSpecification).Distinct().OrderBy(x => x).ToList(),
                ["ProcessStatus"] = all.Select(x => x.ProcessStatus).Distinct().OrderBy(x => x).ToList(),
                ["ReturnDeadline"] = all.Where(x => x.ReturnDeadline != null)
                    .Select(x => x.ReturnDeadline!.Value.ToString("yyyy-MM-dd"))
                    .Distinct().OrderBy(x => x).ToList(),
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    public async Task<byte[]> PrintReturnItemListAsync(string? keyword, string? sortBy, bool isDescending, string? status, string? filters, List<PrintColumnDef>? columns)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = 10000,
            Keyword = keyword,
            SortBy = sortBy ?? "Id",
            IsDescending = isDescending
        };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }

        var result = await GetReturnItemListAsync(query, status);

        var resolvers = new Dictionary<string, Func<object?, string>>
        {
            ["ProcessStatus"] = v => v is string ps
                ? (EnumHelper.TryParse<SubcontractOrderStatus>(ps) is { } parsed
                    ? EnumHelper.GetDisplayName(parsed)
                    : ps)
                : "-"
        };

        return TablePrintHelper.GeneratePdf("子项查询", result.Items, columns ?? new List<PrintColumnDef>(), resolvers);
    }

    public async Task<byte[]> PrintReturnItemSelectedAsync(int[] ids, List<PrintColumnDef>? columns)
    {
        var items = await _context.SubcontractReturnItems
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .Select(i => new SubcontractReturnItemListDto
            {
                Id = i.Id,
                SubcontractOrderId = i.SubcontractOrderId,
                OrderNo = i.OrderNo ?? i.SubcontractOrder.OrderNo,
                SupplierName = i.SubcontractOrder.SupplierName,
                SourceWorkOrderNo = i.SourceWorkOrderNo,
                PlantGrade = i.PlantGrade,
                ProcessSpecification = i.ProcessSpecification,
                UnitWeight = i.UnitWeight,
                RequiredQuantity = i.RequiredQuantity,
                RequiredWeight = i.RequiredWeight,
                ReturnDeadline = i.SubcontractOrder.ReturnDeadline,
                ReturnedQuantity = i.ReturnedQuantity,
                ReturnedWeight = i.ReturnedWeight,
                ProcessStatus = i.ProcessStatus
            })
            .ToListAsync();

        var resolvers = new Dictionary<string, Func<object?, string>>
        {
            ["ProcessStatus"] = v => v is string ps
                ? (EnumHelper.TryParse<SubcontractOrderStatus>(ps) is { } parsed
                    ? EnumHelper.GetDisplayName(parsed)
                    : ps)
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

        return entities.Select(e =>
        {
            var dto = ToDto(e);

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

    private async Task RecalcSubcontractStatusAsync(SubcontractOrder order)
    {
        var ratio = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteRatio", 0.965m);
        var deviation = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteDeviation", 200m);

        if (order.InWeight == null || order.InWeight == 0)
            order.Status = SubcontractOrderStatus.Sent;
        else if (PurchaseOrderService.IsThresholdMet(order.InWeight.Value, order.OutWeight, ratio, deviation))
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
        CreatedTime = entity.CreatedTime
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
