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
using MES.Core.DTOs.WorkOrder;
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
using MES.Core.Helpers;
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
using MES.Core.Enums;
using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Materials;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly AppDbContext _context;
    private readonly IConfigParameterService _configService;
    private readonly IWorkOrderExecutionService _workOrderExecutionService;
    private readonly ILogger<PurchaseOrderService> _logger;
    private readonly Dictionary<string, Dictionary<string, decimal>> _configMaps = new();
    private readonly IMemoryCache _cache;

    public PurchaseOrderService(AppDbContext context, IConfigParameterService configService,
        IWorkOrderExecutionService workOrderExecutionService, ILogger<PurchaseOrderService> logger, IMemoryCache cache)
    {
        _context = context;
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

    public async Task<PagedResult<PurchaseOrderDto>> GetPagedAsync(PurchaseOrderQueryParams query)
    {
        // 实体级查询（MaterialCategory 为字符串，用于 DB 端筛选和排序）
        var entityQuery = from p in _context.PurchaseOrders.AsNoTracking()
                          join w in _context.WorkOrders.AsNoTracking() on p.SourceWorkOrderNo equals w.WorkOrderNo into wj
                          from w in wj.DefaultIfEmpty()
                          select new { p, w, MaterialCategory = p.MaterialCategory };

        // 关键词筛选（使用实体级字符串字段）
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            entityQuery = entityQuery.Where(x =>
                x.p.OrderNo.Contains(kw) ||
                x.p.MaterialCategory.Contains(kw) ||
                x.p.PlantGrade.Contains(kw) ||
                x.p.Specification.Contains(kw) ||
                (x.p.SupplierName != null && x.p.SupplierName.Contains(kw)) ||
                (x.p.SourceWorkOrderNo != null && x.p.SourceWorkOrderNo.Contains(kw)) ||
                (x.w.SalesOrderNo != null && x.w.SalesOrderNo.Contains(kw)) ||
                (x.w.ProductionMainNo != null && x.w.ProductionMainNo.Contains(kw)) ||
                (x.w.ProductionSubNo != null && x.w.ProductionSubNo.Contains(kw)) ||
                (x.w.Salesman != null && x.w.Salesman.Contains(kw)) ||
                (x.w.EndCustomer != null && x.w.EndCustomer.Contains(kw)) ||
                (x.w.PlantGrade != null && x.w.PlantGrade.Contains(kw)) ||
                (x.w.Specification != null && x.w.Specification.Contains(kw)) ||
                (x.p.Remark != null && x.p.Remark.Contains(kw)));
        }

        // 状态筛选
        if (!string.IsNullOrEmpty(query.Status) && Enum.TryParse<PurchaseOrderStatus>(query.Status, out var parsedStatus))
        {
            entityQuery = entityQuery.Where(x => x.p.Status == parsedStatus);
        }

        // 下单日期筛选
        if (query.DateFrom.HasValue)
        {
            var from = query.DateFrom.Value.Date;
            entityQuery = entityQuery.Where(x => x.p.OrderDate >= from);
        }
        if (query.DateTo.HasValue)
        {
            var to = query.DateTo.Value.Date.AddDays(1);
            entityQuery = entityQuery.Where(x => x.p.OrderDate < to);
        }

        // 要求到货日筛选
        if (query.RequiredDateFrom.HasValue)
        {
            var from = query.RequiredDateFrom.Value.Date;
            entityQuery = entityQuery.Where(x => x.p.RequiredDate >= from);
        }
        if (query.RequiredDateTo.HasValue)
        {
            var to = query.RequiredDateTo.Value.Date.AddDays(1);
            entityQuery = entityQuery.Where(x => x.p.RequiredDate < to);
        }

        // 通用筛选 + 排序（通过 DTO 级查询桥接）
        var dtoQuery = entityQuery.Select(x => new
        {
            // 所有需要排序/筛选的字段（MaterialCategory 仍用字符串代理）
            x.p.Id, x.p.OrderNo, x.p.SupplierId, x.p.SupplierName,
            x.p.OrderDate, x.p.Status, x.p.IsForceCompleted, x.MaterialCategory,
            x.p.PlantGrade, x.p.Specification, x.p.UnitWeight, x.p.Quantity,
            x.p.Weight, x.p.RequiredDate, x.p.UnitPrice, x.p.TotalAmount,
            x.p.LastArrivalDate, x.p.ReceivedQuantity, x.p.ReceivedWeight,
            x.p.SourceWorkOrderNo, x.p.InputMultiple, x.p.Remark, x.p.CreatedTime,
            WoSalesOrderNo = x.w != null ? x.w.SalesOrderNo : null,
            WoProductionMainNo = x.w != null ? x.w.ProductionMainNo : null,
            WoProductionSubNo = x.w != null ? x.w.ProductionSubNo : null,
            WoSignDate = x.w != null ? (DateTime?)x.w.SignDate : null,
            WoSalesman = x.w != null ? x.w.Salesman : null,
            WoEndCustomer = x.w != null ? x.w.EndCustomer : null,
            WoDeliveryDate = x.w != null ? (DateTime?)x.w.DeliveryDate : null,
            WoDelayPenalty = x.w != null && x.w.DelayPenalty,
            WoSettlementMethod = x.w != null ? (SettlementMethod?)x.w.SettlementMethod : null,
            WoPlantGrade = x.w != null ? x.w.PlantGrade : null,
            WoSpecification = x.w != null ? x.w.Specification : null,
            WoLengthStatus = x.w != null ? (LengthStatus?)x.w.LengthStatus : null,
            WoMaxLength = x.w != null ? x.w.MaxLength : null,
            WoTotalQuantity = x.w != null ? (int?)x.w.TotalQuantity : null,
            WoTotalWeight = x.w != null ? (decimal?)x.w.TotalWeight : null,
            WoDeliveryState = x.w != null ? (DeliveryState?)x.w.DeliveryState : null,
            WoTotalItemCount = x.w != null ? (int?)x.w.TotalItemCount : null,
        });

        // 通用筛选
        dtoQuery = dtoQuery.ApplyFilters(query.Filters);

        var totalCount = await dtoQuery.CountAsync();

        // 排序（基于 DTO 字段名）
        dtoQuery = (query.SortBy?.ToLower(), query.IsDescending) switch
        {
            ("orderdate", false) => dtoQuery.OrderBy(x => x.OrderDate),
            ("orderdate", true) => dtoQuery.OrderByDescending(x => x.OrderDate),
            ("materialcategory", false) => dtoQuery.OrderBy(x => x.MaterialCategory),
            ("materialcategory", true) => dtoQuery.OrderByDescending(x => x.MaterialCategory),
            _ => query.IsDescending
                ? dtoQuery.OrderByDescending(x => x.CreatedTime)
                : dtoQuery.OrderBy(x => x.CreatedTime)
        };

        var items = await dtoQuery
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        var dtos = items.Select(x => new PurchaseOrderDto
        {
            Id = x.Id,
            OrderNo = x.OrderNo,
            SupplierId = x.SupplierId,
            SupplierName = x.SupplierName ?? "",
            OrderDate = x.OrderDate,
            Status = x.Status,
            IsForceCompleted = x.IsForceCompleted,
            MaterialCategory = !string.IsNullOrEmpty(x.MaterialCategory) && Enum.TryParse<MaterialType>(x.MaterialCategory, out var mc) ? mc : default,
            PlantGrade = x.PlantGrade,
            Specification = x.Specification,
            UnitWeight = x.UnitWeight,
            Quantity = x.Quantity,
            Weight = x.Weight,
            RequiredDate = x.RequiredDate,
            UnitPrice = x.UnitPrice,
            TotalAmount = x.TotalAmount,
            LastArrivalDate = x.LastArrivalDate,
            ReceivedQuantity = x.ReceivedQuantity,
            ReceivedWeight = x.ReceivedWeight,
            SourceWorkOrderNo = x.SourceWorkOrderNo,
            InputMultiple = x.InputMultiple,
            Remark = x.Remark,
            CreatedTime = x.CreatedTime,
            WoSalesOrderNo = x.WoSalesOrderNo,
            WoProductionMainNo = x.WoProductionMainNo,
            WoProductionSubNo = x.WoProductionSubNo,
            WoSignDate = x.WoSignDate,
            WoSalesman = x.WoSalesman,
            WoEndCustomer = x.WoEndCustomer,
            WoDeliveryDate = x.WoDeliveryDate,
            WoDelayPenalty = x.WoDelayPenalty,
            WoSettlementMethod = x.WoSettlementMethod,
            WoPlantGrade = x.WoPlantGrade,
            WoSpecification = x.WoSpecification,
            WoLengthStatus = x.WoLengthStatus,
            WoMaxLength = x.WoMaxLength,
            WoTotalQuantity = x.WoTotalQuantity,
            WoTotalWeight = x.WoTotalWeight,
            WoDeliveryState = x.WoDeliveryState,
            WoTotalItemCount = x.WoTotalItemCount,
        }).ToList();

        return new PagedResult<PurchaseOrderDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<PurchaseOrderDto>> GetAllListAsync()
    {
        var items = await (from p in _context.PurchaseOrders.AsNoTracking()
                           join w in _context.WorkOrders.AsNoTracking() on p.SourceWorkOrderNo equals w.WorkOrderNo into wj
                           from w in wj.DefaultIfEmpty()
                           orderby p.OrderDate, p.OrderNo
                           select new
                           {
                               p, w, MaterialCategory = p.MaterialCategory
                           }).ToListAsync();

        return items.Select(x => new PurchaseOrderDto
        {
            Id = x.p.Id,
            OrderNo = x.p.OrderNo,
            SupplierId = x.p.SupplierId,
            SupplierName = x.p.SupplierName ?? "",
            OrderDate = x.p.OrderDate,
            Status = x.p.Status,
            IsForceCompleted = x.p.IsForceCompleted,
            MaterialCategory = !string.IsNullOrEmpty(x.MaterialCategory) && Enum.TryParse<MaterialType>(x.MaterialCategory, out var mc) ? mc : default,
            PlantGrade = x.p.PlantGrade,
            Specification = x.p.Specification,
            UnitWeight = x.p.UnitWeight,
            Quantity = x.p.Quantity,
            Weight = x.p.Weight,
            RequiredDate = x.p.RequiredDate,
            UnitPrice = x.p.UnitPrice,
            TotalAmount = x.p.TotalAmount,
            LastArrivalDate = x.p.LastArrivalDate,
            ReceivedQuantity = x.p.ReceivedQuantity,
            ReceivedWeight = x.p.ReceivedWeight,
            SourceWorkOrderNo = x.p.SourceWorkOrderNo,
            InputMultiple = x.p.InputMultiple,
            Remark = x.p.Remark,
            CreatedTime = x.p.CreatedTime,
            WoSalesOrderNo = x.w?.SalesOrderNo,
            WoProductionMainNo = x.w?.ProductionMainNo,
            WoProductionSubNo = x.w?.ProductionSubNo,
            WoSignDate = x.w != null ? (DateTime?)x.w.SignDate : null,
            WoSalesman = x.w?.Salesman,
            WoEndCustomer = x.w?.EndCustomer,
            WoDeliveryDate = x.w != null ? (DateTime?)x.w.DeliveryDate : null,
            WoDelayPenalty = x.w != null && x.w.DelayPenalty,
            WoSettlementMethod = x.w != null ? (SettlementMethod?)x.w.SettlementMethod : null,
            WoPlantGrade = x.w?.PlantGrade,
            WoSpecification = x.w?.Specification,
            WoLengthStatus = x.w != null ? (LengthStatus?)x.w.LengthStatus : null,
            WoMaxLength = x.w?.MaxLength,
            WoTotalQuantity = x.w != null ? (int?)x.w.TotalQuantity : null,
            WoTotalWeight = x.w != null ? (decimal?)x.w.TotalWeight : null,
            WoDeliveryState = x.w != null ? (DeliveryState?)x.w.DeliveryState : null,
            WoTotalItemCount = x.w != null ? (int?)x.w.TotalItemCount : null,
        }).ToList();
    }

    public async Task<PurchaseOrderDto> GetByIdAsync(int id)
    {
        var item = await (from p in _context.PurchaseOrders.AsNoTracking()
                          join w in _context.WorkOrders.AsNoTracking() on p.SourceWorkOrderNo equals w.WorkOrderNo into wj
                          from w in wj.DefaultIfEmpty()
                          where p.Id == id
                          select new { p, w, MaterialCategory = p.MaterialCategory }).FirstOrDefaultAsync();

        if (item == null) throw new BusinessException("采购单不存在");

        return new PurchaseOrderDto
        {
            Id = item.p.Id,
            OrderNo = item.p.OrderNo,
            SupplierId = item.p.SupplierId,
            SupplierName = item.p.SupplierName ?? "",
            OrderDate = item.p.OrderDate,
            Status = item.p.Status,
            IsForceCompleted = item.p.IsForceCompleted,
            MaterialCategory = !string.IsNullOrEmpty(item.MaterialCategory) && Enum.TryParse<MaterialType>(item.MaterialCategory, out var mc) ? mc : default,
            PlantGrade = item.p.PlantGrade,
            Specification = item.p.Specification,
            UnitWeight = item.p.UnitWeight,
            Quantity = item.p.Quantity,
            Weight = item.p.Weight,
            RequiredDate = item.p.RequiredDate,
            UnitPrice = item.p.UnitPrice,
            TotalAmount = item.p.TotalAmount,
            LastArrivalDate = item.p.LastArrivalDate,
            ReceivedQuantity = item.p.ReceivedQuantity,
            ReceivedWeight = item.p.ReceivedWeight,
            SourceWorkOrderNo = item.p.SourceWorkOrderNo,
            InputMultiple = item.p.InputMultiple,
            Remark = item.p.Remark,
            CreatedTime = item.p.CreatedTime,
            WoSalesOrderNo = item.w?.SalesOrderNo,
            WoProductionMainNo = item.w?.ProductionMainNo,
            WoProductionSubNo = item.w?.ProductionSubNo,
            WoSignDate = (DateTime?)item.w?.SignDate,
            WoSalesman = item.w?.Salesman,
            WoEndCustomer = item.w?.EndCustomer,
            WoDeliveryDate = (DateTime?)item.w?.DeliveryDate,
            WoDelayPenalty = item.w != null && item.w.DelayPenalty,
            WoSettlementMethod = item.w != null ? (SettlementMethod?)item.w.SettlementMethod : null,
            WoPlantGrade = item.w?.PlantGrade,
            WoSpecification = item.w?.Specification,
            WoLengthStatus = item.w != null ? (LengthStatus?)item.w.LengthStatus : null,
            WoMaxLength = item.w?.MaxLength,
            WoTotalQuantity = (int?)item.w?.TotalQuantity,
            WoTotalWeight = (decimal?)item.w?.TotalWeight,
            WoDeliveryState = item.w != null ? (DeliveryState?)item.w.DeliveryState : null,
            WoTotalItemCount = (int?)item.w?.TotalItemCount,
        };
    }

    public async Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request)
    {
        // Serializable事务：防止并发读取到相同maxSeq导致唯一键冲突
        PurchaseOrder entity = null!;
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

                entity = new PurchaseOrder
                {
                    OrderNo = orderNo,
                    SupplierId = request.SupplierId,
                    SupplierName = supplierName,
                    OrderDate = request.OrderDate,
                    MaterialCategory = request.MaterialCategory.ToString(),
                    PlantGrade = request.PlantGrade,
                    Specification = request.Specification,
                    UnitWeight = request.UnitWeight,
                    Quantity = request.Quantity,
                    Weight = request.Weight,
                    RequiredDate = request.RequiredDate,
                    UnitPrice = request.UnitPrice,
                    SourceWorkOrderNo = request.SourceWorkOrderNo,
                    InputMultiple = request.InputMultiple,
                    Remark = request.Remark
                };

                // 计算总金额
                if (request.Quantity.HasValue && request.UnitPrice.HasValue)
                    entity.TotalAmount = request.Quantity.Value * request.UnitPrice.Value;

                _context.PurchaseOrders.Add(entity);
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
        await TryRefreshExecutionSummaryAsync(entity.SourceWorkOrderNo);
        return dto;
    }

    public async Task<List<PurchaseOrderDto>> CreateBatchAsync(List<CreatePurchaseOrderRequest> requests)
    {
        if (requests == null || requests.Count == 0)
            throw new BusinessException("请求列表不能为空");

        // Serializable事务：防止并发读取到相同maxSeq导致唯一键冲突
        var entities = new List<PurchaseOrder>();
        var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        using (transaction)
        {
            try
            {
                // 一次查询，批量生成所有唯一单号（基于数值计算，避免字符串排序的009>010问题）
                var today = DateTime.Now.ToString("yyMMdd");
                var prefix = $"CG{today}";
                var existingNos = await _context.PurchaseOrders
                    .Where(p => p.OrderNo.StartsWith(prefix) && p.OrderNo.Length == prefix.Length + 3)
                    .Select(p => p.OrderNo)
                    .ToListAsync();

                int maxSeq = 0;
                foreach (var no in existingNos)
                {
                    if (int.TryParse(no[^3..], out var s) && s > maxSeq)
                        maxSeq = s;
                }
                int seq = maxSeq + 1;

                // 批量查询供应商名称
                var supplierIdsBatch = requests.Select(r => r.SupplierId).Distinct().ToList();
                var supplierNames = await _context.SupplierProfiles
                    .Where(s => supplierIdsBatch.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.SupplierName);

                foreach (var request in requests)
                {
                    var orderNo = $"{prefix}{seq:D3}";
                    seq++;

                    var entity = new PurchaseOrder
                    {
                        OrderNo = orderNo,
                        SupplierId = request.SupplierId,
                        SupplierName = supplierNames.GetValueOrDefault(request.SupplierId),
                        OrderDate = request.OrderDate,
                        MaterialCategory = request.MaterialCategory.ToString(),
                        PlantGrade = request.PlantGrade,
                        Specification = request.Specification,
                        UnitWeight = request.UnitWeight,
                        Quantity = request.Quantity,
                        Weight = request.Weight,
                        RequiredDate = request.RequiredDate,
                        UnitPrice = request.UnitPrice,
                        SourceWorkOrderNo = request.SourceWorkOrderNo,
                        InputMultiple = request.InputMultiple,
                        Remark = request.Remark
                    };

                    if (request.Quantity.HasValue && request.UnitPrice.HasValue)
                        entity.TotalAmount = request.Quantity.Value * request.UnitPrice.Value;

                    _context.PurchaseOrders.Add(entity);
                    entities.Add(entity);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        var distinctWoNos = entities
            .Select(e => e.SourceWorkOrderNo)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Distinct()
            .ToList();
        foreach (var woNo in distinctWoNos) await TryRefreshExecutionSummaryAsync(woNo);

        return entities.Select(ToDto).ToList();
    }

    public async Task<PurchaseOrderDto> UpdateAsync(int id, UpdatePurchaseOrderRequest request, bool isAdmin = false)
    {
        var entity = await _context.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) throw new BusinessException("采购单不存在");

        // 管理员可无视状态编辑任何采购单
        if (isAdmin)
        {
            var oldSupplierId = entity.SupplierId;
            MapUpdateFields(entity, request);
            if (entity.SupplierId != oldSupplierId)
            {
                entity.SupplierName = await _context.SupplierProfiles
                    .Where(s => s.Id == entity.SupplierId)
                    .Select(s => s.SupplierName)
                    .FirstOrDefaultAsync();
            }
            await _context.SaveChangesAsync();

            var dto = ToDto(entity);
            await TryRefreshExecutionSummaryAsync(entity.SourceWorkOrderNo);
            return dto;
        }

        if (entity.Status == PurchaseOrderStatus.Completed)
        {
            // 已完成：仅允许修改来源工单号
            entity.SourceWorkOrderNo = request.SourceWorkOrderNo ?? entity.SourceWorkOrderNo;
        }
        else
        {
            var oldSupplierId = entity.SupplierId;
            MapUpdateFields(entity, request);
            if (entity.SupplierId != oldSupplierId)
            {
                entity.SupplierName = await _context.SupplierProfiles
                    .Where(s => s.Id == entity.SupplierId)
                    .Select(s => s.SupplierName)
                    .FirstOrDefaultAsync();
            }

            // 非强制完成时自动计算状态
            if (!entity.IsForceCompleted)
            {
                var ratio = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteRatio", 0.965m);
                var deviation = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteDeviation", 200m);
                RecalcPurchaseStatus(entity, ratio, deviation);
            }
        }

        await _context.SaveChangesAsync();

        var dto2 = ToDto(entity);
        await TryRefreshExecutionSummaryAsync(entity.SourceWorkOrderNo);
        return dto2;
    }

    private static void MapUpdateFields(PurchaseOrder entity, UpdatePurchaseOrderRequest request)
    {
        entity.SupplierId = request.SupplierId;
        entity.MaterialCategory = request.MaterialCategory.ToString();
        entity.PlantGrade = request.PlantGrade;
        entity.Specification = request.Specification;
        entity.UnitWeight = request.UnitWeight ?? entity.UnitWeight;
        entity.Quantity = request.Quantity ?? entity.Quantity;
        entity.Weight = request.Weight;
        entity.RequiredDate = request.RequiredDate;
        entity.UnitPrice = request.UnitPrice ?? entity.UnitPrice;
        entity.InputMultiple = request.InputMultiple;
        entity.SourceWorkOrderNo = request.SourceWorkOrderNo ?? entity.SourceWorkOrderNo;
        entity.Remark = request.Remark ?? entity.Remark;

        // 重新计算总金额
        if (request.Quantity.HasValue && request.UnitPrice.HasValue)
            entity.TotalAmount = request.Quantity.Value * request.UnitPrice.Value;
        else
            entity.TotalAmount = null;
    }

    public async Task SyncAllAsync()
    {
        var orders = await _context.PurchaseOrders
            .ToListAsync();

        var orderNos = orders.Select(o => o.OrderNo).ToList();
        var batches = await _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.SourceOrderNo != null && orderNos.Contains(b.SourceOrderNo))
            .ToListAsync();

        foreach (var order in orders)
        {
            var orderBatches = batches.Where(b => string.Equals(b.SourceOrderNo, order.OrderNo, StringComparison.OrdinalIgnoreCase)).ToList();
            if (orderBatches.Count == 0) continue;

            order.ReceivedQuantity = orderBatches.Sum(b => b.InitialQuantity);
            order.ReceivedWeight = orderBatches.Sum(b => b.InitialWeight);
            order.LastArrivalDate = orderBatches.Max(b => b.InboundDate);

            if (!order.IsForceCompleted)
            {
                var ratio = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteRatio", 0.965m);
                var deviation = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteDeviation", 200m);
                RecalcPurchaseStatus(order, ratio, deviation);
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task SyncSingleAsync(int id)
    {
        var order = await _context.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == id);
        if (order == null) throw new BusinessException("采购单不存在");

        var batches = await _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.SourceOrderNo == order.OrderNo)
            .ToListAsync();

        order.ReceivedQuantity = batches.Sum(b => b.InitialQuantity);
        order.ReceivedWeight = batches.Sum(b => b.InitialWeight);
        order.LastArrivalDate = batches.Count > 0 ? batches.Max(b => b.InboundDate) : null;

        if (!order.IsForceCompleted)
        {
            var ratio = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteRatio", 0.965m);
            var deviation = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteDeviation", 200m);
            RecalcPurchaseStatus(order, ratio, deviation);
        }

        await _context.SaveChangesAsync();
        await TryRefreshExecutionSummaryAsync(order.SourceWorkOrderNo);
    }

    public async Task UpdateStatusAsync(int id, UpdateOrderStatusRequest request)
    {
        var entity = await _context.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) throw new BusinessException("采购单不存在");

        entity.IsForceCompleted = request.IsForceCompleted;

        if (entity.IsForceCompleted)
            entity.Status = PurchaseOrderStatus.Completed;
        else
        {
            var ratio = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteRatio", 0.965m);
            var deviation = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteDeviation", 200m);
            RecalcPurchaseStatus(entity, ratio, deviation);
        }

        await _context.SaveChangesAsync();
        await TryRefreshExecutionSummaryAsync(entity.SourceWorkOrderNo);
    }

    public async Task DeleteAsync(int id, bool isAdmin = false)
    {
        var entity = await _context.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) throw new BusinessException("采购单不存在");

        // 管理员可无视状态删除任何采购单
        if (!isAdmin)
        {
            if (entity.Status == PurchaseOrderStatus.Completed) throw new BusinessException("已完成的采购单无法删除");
        }

        var deletedWoNo = entity.SourceWorkOrderNo;
        _context.PurchaseOrders.Remove(entity);
        await _context.SaveChangesAsync();
        await TryRefreshExecutionSummaryAsync(deletedWoNo);
    }

    private static void RecalcPurchaseStatus(PurchaseOrder order, decimal purchaseCompleteRatio, decimal purchaseCompleteDeviation)
    {
        if (order.ReceivedWeight == 0)
            order.Status = PurchaseOrderStatus.Open;
        else if (IsThresholdMet(order.ReceivedWeight, order.Weight, purchaseCompleteRatio, purchaseCompleteDeviation))
            order.Status = PurchaseOrderStatus.Completed;
        else
            order.Status = PurchaseOrderStatus.Partial;
    }

    private async Task<string> GenerateOrderNoAsync()
    {
        var today = DateTime.Now.ToString("yyMMdd");
        var prefix = $"CG{today}";
        var existingNos = await _context.PurchaseOrders
            .Where(p => p.OrderNo.StartsWith(prefix) && p.OrderNo.Length == prefix.Length + 3)
            .Select(p => p.OrderNo)
            .ToListAsync();

        int maxSeq = 0;
        foreach (var no in existingNos)
        {
            if (int.TryParse(no[^3..], out var s) && s > maxSeq)
                maxSeq = s;
        }

        return $"{prefix}{maxSeq + 1:D3}";
    }

    private static PurchaseOrderDto ToDto(PurchaseOrder entity) => new()
    {
        Id = entity.Id,
        OrderNo = entity.OrderNo,
        SupplierId = entity.SupplierId,
        SupplierName = entity.SupplierName ?? "",
        OrderDate = entity.OrderDate,
        Status = entity.Status,
        IsForceCompleted = entity.IsForceCompleted,
        MaterialCategory = !string.IsNullOrEmpty(entity.MaterialCategory) && Enum.TryParse<MaterialType>(entity.MaterialCategory, out var mc) ? mc : default,
        PlantGrade = entity.PlantGrade,
        Specification = entity.Specification,
        UnitWeight = entity.UnitWeight,
        Quantity = entity.Quantity,
        Weight = entity.Weight,
        RequiredDate = entity.RequiredDate,
        UnitPrice = entity.UnitPrice,
        TotalAmount = entity.TotalAmount,
        LastArrivalDate = entity.LastArrivalDate,
        ReceivedQuantity = entity.ReceivedQuantity,
        ReceivedWeight = entity.ReceivedWeight,
        SourceWorkOrderNo = entity.SourceWorkOrderNo,
        InputMultiple = entity.InputMultiple,
        Remark = entity.Remark,
        CreatedTime = entity.CreatedTime
    };

    public async Task<List<ProcurementStatusDto>> GetProcurementStatusAsync()
    {
        var purchaseCompleteRatio = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteRatio", 0.965m);
        var purchaseCompleteDeviation = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteDeviation", 200m);

        // 1. 分别查询两个表的 WorkOrderId（避免 Union 不同 DbSet 导致 EF Core 翻译失败）
        var semiWorkOrderIds = await _context.PurchaseSemiPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0)
            .Select(p => p.WorkOrderId)
            .Distinct()
            .ToListAsync();

        var finishedWorkOrderIds = await _context.PurchaseFinishedPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0)
            .Select(p => p.WorkOrderId)
            .Distinct()
            .ToListAsync();

        var workOrderIds = semiWorkOrderIds.Union(finishedWorkOrderIds).ToList();
        if (workOrderIds.Count == 0)
            return new List<ProcurementStatusDto>();

        // 2. 获取工单号映射
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(w => workOrderIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.WorkOrderNo);

        var allWorkOrderNos = workOrders.Values.ToList();

        // 3. 原料采购计划：按工单号+原料类型汇总
        var semiPlanData = await _context.PurchaseSemiPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0 && workOrderIds.Contains(p.WorkOrderId))
            .GroupBy(p => new { p.WorkOrderId, p.RawMaterialType })
            .Select(g => new
            {
                g.Key.WorkOrderId,
                CategoryName = g.Key.RawMaterialType == MaterialType.RoughTube ? "RoughTube" : "SemiFinished",
                PlanWeight = g.Sum(p => p.RequiredWeight)
            })
            .ToListAsync();

        // 4. 成品采购计划：按工单号+成品类型汇总
        var finishedPlanData = await _context.PurchaseFinishedPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0 && workOrderIds.Contains(p.WorkOrderId))
            .GroupBy(p => new { p.WorkOrderId, p.ProductType })
            .Select(g => new
            {
                g.Key.WorkOrderId,
                CategoryName = g.Key.ProductType == FinishedProductType.Critical ? "CriticalFinished" : "OrderFinished",
                PlanWeight = g.Sum(p => p.RequiredWeight)
            })
            .ToListAsync();

        // 5. 按工单号聚合已采购重量（按采购单 Weight 汇总）
        var purchaseWeights = new Dictionary<string, decimal>();
        if (allWorkOrderNos.Count > 0)
        {
            var purchaseData = await _context.PurchaseOrders
                .AsNoTracking()
                .Where(p => p.SourceWorkOrderNo != null && allWorkOrderNos.Contains(p.SourceWorkOrderNo))
                .GroupBy(p => p.SourceWorkOrderNo!)
                .Select(g => new { WorkOrderNo = g.Key, Weight = g.Sum(p => p.Weight) })
                .ToListAsync();
            purchaseWeights = purchaseData.ToDictionary(x => x.WorkOrderNo, x => x.Weight, StringComparer.OrdinalIgnoreCase);
        }

        // 6. 按工单号+物料分类聚合已委外重量（按 ReturnItems RequiredWeight 汇总）
        var subcontractWeights = new Dictionary<string, decimal>();
        if (allWorkOrderNos.Count > 0)
        {
            var subcontractData = await _context.SubcontractReturnItems
                .AsNoTracking()
                .Where(r => r.SourceWorkOrderNo != null && allWorkOrderNos.Contains(r.SourceWorkOrderNo))
                .GroupBy(r => new { SourceWorkOrderNo = r.SourceWorkOrderNo!, r.MaterialCategory })
                .Select(g => new { g.Key.SourceWorkOrderNo, g.Key.MaterialCategory, Weight = g.Sum(r => r.RequiredWeight ?? 0) })
                .ToListAsync();
            subcontractWeights = subcontractData
                .ToDictionary(x => $"{x.SourceWorkOrderNo}|{x.MaterialCategory}", x => x.Weight, StringComparer.OrdinalIgnoreCase);
        }

        // 7. 合并原料+成品计划数据，计算采购执行状态
        var allPlanData = semiPlanData.Concat(finishedPlanData)
            .Select(x =>
            {
                var workOrderNo = workOrders.GetValueOrDefault(x.WorkOrderId, "");
                var purchaseW = purchaseWeights.GetValueOrDefault(workOrderNo, 0);
                var subcontractW = subcontractWeights.GetValueOrDefault($"{workOrderNo}|{x.CategoryName}", 0);
                var total = purchaseW + subcontractW;
                return new ProcurementStatusDto
                {
                    WorkOrderNo = workOrderNo,
                    MaterialName = workOrderNo,
                    MaterialCategory = x.CategoryName,
                    PlanWeight = x.PlanWeight,
                    PurchaseWeight = purchaseW,
                    SubcontractWeight = subcontractW,
                    TotalWeight = total,
                    StatusText = total == 0 ? "未采购"
                        : IsThresholdMet(total, x.PlanWeight, purchaseCompleteRatio, purchaseCompleteDeviation) ? "已采购"
                        : "部分采购"
                };
            })
            .Where(x => x.StatusText != "已采购" && !string.IsNullOrEmpty(x.WorkOrderNo))
            .OrderBy(x => x.WorkOrderNo)
            .ThenBy(x => x.MaterialCategory)
            .ToList();

        return allPlanData;
    }

    public async Task<List<ProcurementStatusDto>> GetPiercingProcurementStatusAsync()
    {
        var purchaseCompleteRatio = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteRatio", 0.965m);
        var purchaseCompleteDeviation = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteDeviation", 200m);

        // 1. 查询圆棒穿孔计划的工单ID
        var piercingWorkOrderIds = await _context.RoundBarPiercingPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0)
            .Select(p => p.WorkOrderId)
            .Distinct()
            .ToListAsync();

        if (piercingWorkOrderIds.Count == 0)
            return new List<ProcurementStatusDto>();

        // 2. 获取工单号映射
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(w => piercingWorkOrderIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.WorkOrderNo);

        var allWorkOrderNos = workOrders.Values.ToList();

        // 3. 圆棒穿孔计划：按工单号汇总
        var piercingPlanData = await _context.RoundBarPiercingPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0 && piercingWorkOrderIds.Contains(p.WorkOrderId))
            .GroupBy(p => p.WorkOrderId)
            .Select(g => new
            {
                WorkOrderId = g.Key,
                PlanWeight = g.Sum(p => p.RequiredWeight)
            })
            .ToListAsync();

        // 4. 按工单号聚合已委外重量（按 ReturnItems RequiredWeight 汇总）
        var subcontractWeights = new Dictionary<string, decimal>();
        if (allWorkOrderNos.Count > 0)
        {
            var subcontractData = await _context.SubcontractReturnItems
                .AsNoTracking()
                .Where(r => r.SourceWorkOrderNo != null && allWorkOrderNos.Contains(r.SourceWorkOrderNo))
                .GroupBy(r => r.SourceWorkOrderNo!)
                .Select(g => new { SourceWorkOrderNo = g.Key, Weight = g.Sum(r => r.RequiredWeight ?? 0) })
                .ToListAsync();
            subcontractWeights = subcontractData.ToDictionary(x => x.SourceWorkOrderNo, x => x.Weight, StringComparer.OrdinalIgnoreCase);
        }

        // 5. 合并数据，计算执行状态
        var result = piercingPlanData
            .Select(x =>
            {
                var workOrderNo = workOrders.GetValueOrDefault(x.WorkOrderId, "");
                var subW = subcontractWeights.GetValueOrDefault(workOrderNo, 0);
                return new ProcurementStatusDto
                {
                    WorkOrderNo = workOrderNo,
                    MaterialName = workOrderNo,
                    MaterialCategory = "RoundBar",
                    PlanWeight = x.PlanWeight,
                    PurchaseWeight = 0,
                    SubcontractWeight = subW,
                    TotalWeight = subW,
                    StatusText = subW == 0 ? "未穿孔"
                        : IsThresholdMet(subW, x.PlanWeight, purchaseCompleteRatio, purchaseCompleteDeviation) ? "已穿孔"
                        : "部分穿孔"
                };
            })
            .Where(x => x.StatusText != "已穿孔" && !string.IsNullOrEmpty(x.WorkOrderNo))
            .OrderBy(x => x.WorkOrderNo)
            .ToList();

        return result;
    }

    public async Task<List<OrderMismatchInfo>> GetMismatchedPurchaseOrdersAsync()
    {
        // 1. 获取所有涉及采购的工单号（有用料计划且需采购）
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

        // 2. 查询所有采购单中 SourceWorkOrderNo 不为空的
        var purchaseOrders = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(p => p.SourceWorkOrderNo != null && p.SourceWorkOrderNo != "")
            .Select(p => new { p.OrderNo, p.SourceWorkOrderNo })
            .ToListAsync();

        // 3. 找出不匹配的
        var mismatches = purchaseOrders
            .Where(p => !validWorkOrderNos.Contains(p.SourceWorkOrderNo!))
            .GroupBy(p => p.OrderNo)
            .Select(g => new OrderMismatchInfo
            {
                OrderNo = g.Key,
                MismatchedWorkOrderNos = g.Select(p => p.SourceWorkOrderNo!).Distinct().ToList()
            })
            .ToList();

        return mismatches;
    }

    public async Task<PlanDetailDto?> GetPlanDetailAsync(string workOrderNo, string materialCategory)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.WorkOrderNo == workOrderNo);
        if (workOrder == null) return null;

        // 原料采购（荒管/半成品）
        if (materialCategory == "RoughTube" || materialCategory == "SemiFinished")
        {
            var semiPlan = await _context.PurchaseSemiPlans
                .AsNoTracking()
                .Where(p => p.WorkOrderId == workOrder.Id && p.RequiredWeight > 0)
                .FirstOrDefaultAsync();
            if (semiPlan == null) return null;

            return new PlanDetailDto
            {
                WorkOrderNo = workOrderNo,
                MaterialCategory = materialCategory,
                PlantGrade = semiPlan.PlantGrade,
                Specification = semiPlan.RawMaterialSpec,
                UnitWeight = semiPlan.RequiredUnitWeight,
                Quantity = semiPlan.RequiredPieces,
                Weight = semiPlan.RequiredWeight,
                Remark = semiPlan.Remark,
                RequiredDate = semiPlan.RequiredDate,
                InputMultiple = semiPlan.InputMultiple
            };
        }

        // 成品采购（临界成品/订单成品）
        if (materialCategory == "CriticalFinished" || materialCategory == "OrderFinished")
        {
            var finishedPlan = await _context.PurchaseFinishedPlans
                .AsNoTracking()
                .Where(p => p.WorkOrderId == workOrder.Id && p.RequiredWeight > 0)
                .FirstOrDefaultAsync();
            if (finishedPlan == null) return null;

            return new PlanDetailDto
            {
                WorkOrderNo = workOrderNo,
                MaterialCategory = materialCategory,
                PlantGrade = finishedPlan.PlantGrade,
                Specification = finishedPlan.Specification,
                Quantity = finishedPlan.RequiredPiece,
                Weight = finishedPlan.RequiredWeight,
                Remark = finishedPlan.Remark,
                RequiredDate = finishedPlan.RequiredDate,
                InputMultiple = finishedPlan.InputMultiple
            };
        }

        // 圆棒穿孔
        var piercingPlan = await _context.RoundBarPiercingPlans
            .AsNoTracking()
            .Where(p => p.WorkOrderId == workOrder.Id && p.RequiredWeight > 0)
            .FirstOrDefaultAsync();
        if (piercingPlan == null) return null;

        return new PlanDetailDto
        {
            WorkOrderNo = workOrderNo,
            MaterialCategory = "RoughTube", // 圆棒穿孔实际消耗的是荒管
            PlantGrade = piercingPlan.PlantGrade,
            Specification = piercingPlan.PiercingSpec,
            UnitWeight = piercingPlan.RequiredUnitWeight,
            Quantity = piercingPlan.RequiredPieces,
            Weight = piercingPlan.RequiredWeight,
            Remark = piercingPlan.Remark,
            RequiredDate = piercingPlan.RequiredDate,
            InputMultiple = piercingPlan.InputMultiple
        };
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("PurchaseOrderService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var query = from p in _context.PurchaseOrders.AsNoTracking()
                        join w in _context.WorkOrders.AsNoTracking() on p.SourceWorkOrderNo equals w.WorkOrderNo into wj
                        from w in wj.DefaultIfEmpty()
                        select new
                        {
                            p.OrderNo,
                            p.OrderDate,
                            p.RequiredDate,
                            p.MaterialCategory,
                            p.PlantGrade,
                            p.Specification,
                            p.SourceWorkOrderNo,
                            p.SupplierName,
                            WoSalesOrderNo = w.SalesOrderNo,
                            WoProductionMainNo = w.ProductionMainNo,
                            WoProductionSubNo = w.ProductionSubNo,
                            WoSignDate = (DateTime?)w.SignDate,
                            WoSalesman = w.Salesman,
                            WoEndCustomer = w.EndCustomer,
                            WoDeliveryDate = (DateTime?)w.DeliveryDate,
                            WoPlantGrade = w.PlantGrade,
                            WoSpecification = w.Specification
                        };

            var all = await query.ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["OrderNo"] = all.Select(x => x.OrderNo).Distinct().OrderBy(x => x).ToList(),
                ["SourceWorkOrderNo"] = all.Where(x => x.SourceWorkOrderNo != null).Select(x => x.SourceWorkOrderNo!).Distinct().OrderBy(x => x).ToList(),
                ["OrderDate"] = all.Select(x => x.OrderDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["RequiredDate"] = all.Select(x => x.RequiredDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["MaterialCategory"] = all.Select(x => x.MaterialCategory).Distinct().OrderBy(x => x).ToList(),
                ["PlantGrade"] = all.Select(x => x.PlantGrade).Distinct().OrderBy(x => x).ToList(),
                ["Specification"] = all.Select(x => x.Specification).Distinct().OrderBy(x => x).ToList(),
                ["SupplierName"] = all.Where(x => x.SupplierName != null).Select(x => x.SupplierName!).Distinct().OrderBy(x => x).ToList(),
                ["WoSalesOrderNo"] = all.Where(x => x.WoSalesOrderNo != null).Select(x => x.WoSalesOrderNo!).Distinct().OrderBy(x => x).ToList(),
                ["WoProductionMainNo"] = all.Where(x => x.WoProductionMainNo != null).Select(x => x.WoProductionMainNo!).Distinct().OrderBy(x => x).ToList(),
                ["WoProductionSubNo"] = all.Where(x => x.WoProductionSubNo != null).Select(x => x.WoProductionSubNo!).Distinct().OrderBy(x => x).ToList(),
                ["WoSignDate"] = all.Where(x => x.WoSignDate != null).Select(x => x.WoSignDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["WoSalesman"] = all.Where(x => x.WoSalesman != null).Select(x => x.WoSalesman!).Distinct().OrderBy(x => x).ToList(),
                ["WoEndCustomer"] = all.Where(x => x.WoEndCustomer != null).Select(x => x.WoEndCustomer!).Distinct().OrderBy(x => x).ToList(),
                ["WoDeliveryDate"] = all.Where(x => x.WoDeliveryDate != null).Select(x => x.WoDeliveryDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["WoPlantGrade"] = all.Where(x => x.WoPlantGrade != null).Select(x => x.WoPlantGrade!).Distinct().OrderBy(x => x).ToList(),
                ["WoSpecification"] = all.Where(x => x.WoSpecification != null).Select(x => x.WoSpecification!).Distinct().OrderBy(x => x).ToList(),
            };

        }) ?? new Dictionary<string, List<string>>();
    }

    // ========== 打印 ==========

    public async Task<byte[]> PrintOrderAsync(int id, List<PrintColumnDef>? columns = null)
    {
        var dto = await GetByIdAsync(id);
        return TablePrintHelper.GeneratePdf("采购订单列表", new List<Dictionary<string, object>> { ToPrintDict(dto) }, columns ?? []);
    }

    public async Task<byte[]> PrintOrderBatchAsync(int[] ids, List<PrintColumnDef>? columns = null)
    {
        var orders = await GetByIdsAsync(ids);
        return TablePrintHelper.GeneratePdf("采购订单列表", orders.Select(ToPrintDict).ToList(), columns ?? []);
    }

    public async Task<List<PurchaseOrderDto>> GetByIdsAsync(int[] ids)
    {
        var items = await (from p in _context.PurchaseOrders.AsNoTracking()
                           join w in _context.WorkOrders.AsNoTracking() on p.SourceWorkOrderNo equals w.WorkOrderNo into wj
                           from w in wj.DefaultIfEmpty()
                           where ids.Contains(p.Id)
                           select new { p, w, MaterialCategory = p.MaterialCategory }).ToListAsync();

        return items.Select(x => new PurchaseOrderDto
        {
            Id = x.p.Id,
            OrderNo = x.p.OrderNo,
            SupplierId = x.p.SupplierId,
            SupplierName = x.p.SupplierName ?? "",
            OrderDate = x.p.OrderDate,
            Status = x.p.Status,
            IsForceCompleted = x.p.IsForceCompleted,
            MaterialCategory = !string.IsNullOrEmpty(x.MaterialCategory) && Enum.TryParse<MaterialType>(x.MaterialCategory, out var mc) ? mc : default,
            PlantGrade = x.p.PlantGrade,
            Specification = x.p.Specification,
            UnitWeight = x.p.UnitWeight,
            Quantity = x.p.Quantity,
            Weight = x.p.Weight,
            RequiredDate = x.p.RequiredDate,
            UnitPrice = x.p.UnitPrice,
            TotalAmount = x.p.TotalAmount,
            LastArrivalDate = x.p.LastArrivalDate,
            ReceivedQuantity = x.p.ReceivedQuantity,
            ReceivedWeight = x.p.ReceivedWeight,
            SourceWorkOrderNo = x.p.SourceWorkOrderNo,
            InputMultiple = x.p.InputMultiple,
            Remark = x.p.Remark,
            CreatedTime = x.p.CreatedTime,
            WoSalesOrderNo = x.w?.SalesOrderNo,
            WoProductionMainNo = x.w?.ProductionMainNo,
            WoProductionSubNo = x.w?.ProductionSubNo,
            WoSignDate = x.w != null ? (DateTime?)x.w.SignDate : null,
            WoSalesman = x.w?.Salesman,
            WoEndCustomer = x.w?.EndCustomer,
            WoDeliveryDate = x.w != null ? (DateTime?)x.w.DeliveryDate : null,
            WoDelayPenalty = x.w != null && x.w.DelayPenalty,
            WoSettlementMethod = x.w != null ? (SettlementMethod?)x.w.SettlementMethod : null,
            WoPlantGrade = x.w?.PlantGrade,
            WoSpecification = x.w?.Specification,
            WoLengthStatus = x.w != null ? (LengthStatus?)x.w.LengthStatus : null,
            WoMaxLength = x.w?.MaxLength,
            WoTotalQuantity = x.w != null ? (int?)x.w.TotalQuantity : null,
            WoTotalWeight = x.w != null ? (decimal?)x.w.TotalWeight : null,
            WoDeliveryState = x.w != null ? (DeliveryState?)x.w.DeliveryState : null,
            WoTotalItemCount = x.w != null ? (int?)x.w.TotalItemCount : null,
        }).ToList();
    }

    public async Task<byte[]> PrintOrderAllAsync(string? keyword, string? sortBy = null, bool isDescending = false, DateTime? dateFrom = null, DateTime? dateTo = null, List<PrintColumnDef>? columns = null)
    {
        var query = new PurchaseOrderQueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy ?? "CreatedTime",
            IsDescending = isDescending,
            DateFrom = dateFrom,
            DateTo = dateTo
        };
        var paged = await GetPagedAsync(query);
        return TablePrintHelper.GeneratePdf("采购订单列表", paged.Items.Select(ToPrintDict).ToList(), columns ?? []);
    }

    private static Dictionary<string, object> ToPrintDict(PurchaseOrderDto dto) => new()
    {
        ["OrderNo"] = dto.OrderNo,
        ["OrderDate"] = dto.OrderDate,
        ["SourceWorkOrderNo"] = (object?)dto.SourceWorkOrderNo ?? "",
        ["MaterialCategory"] = EnumHelper.GetDisplayName(dto.MaterialCategory),
        ["PlantGrade"] = dto.PlantGrade,
        ["Specification"] = dto.Specification,
        ["UnitWeight"] = (object?)dto.UnitWeight ?? "",
        ["Quantity"] = (object?)dto.Quantity ?? "",
        ["InputMultiple"] = (object?)dto.InputMultiple ?? "",
        ["Weight"] = dto.Weight,
        ["RequiredDate"] = dto.RequiredDate,
        ["SupplierName"] = dto.SupplierName,
        ["Status"] = dto.Status,
        ["Received"] = $"{dto.ReceivedQuantity}支/{dto.ReceivedWeight:G29}kg",
        // 来源销售订单字段
        ["WoSalesOrderNo"] = (object?)dto.WoSalesOrderNo ?? "",
        ["WoProductionMainNo"] = (object?)dto.WoProductionMainNo ?? "",
        ["WoProductionSubNo"] = (object?)dto.WoProductionSubNo ?? "",
        ["WoSignDate"] = (object?)dto.WoSignDate ?? "",
        ["WoSalesman"] = (object?)dto.WoSalesman ?? "",
        ["WoEndCustomer"] = (object?)dto.WoEndCustomer ?? "",
        ["WoDeliveryDate"] = (object?)dto.WoDeliveryDate ?? "",
        ["WoDelayPenalty"] = dto.WoDelayPenalty,
        ["WoSettlementMethod"] = (object?)dto.WoSettlementMethod ?? "",
        ["WoPlantGrade"] = (object?)dto.WoPlantGrade ?? "",
        ["WoSpecification"] = (object?)dto.WoSpecification ?? "",
        ["WoLengthStatus"] = (object?)dto.WoLengthStatus ?? "",
        ["WoMaxLength"] = (object?)dto.WoMaxLength ?? "",
        ["WoTotalQuantity"] = (object?)dto.WoTotalQuantity ?? "",
        ["WoTotalWeight"] = (object?)dto.WoTotalWeight ?? "",
        ["WoDeliveryState"] = (object?)dto.WoDeliveryState ?? "",
        ["WoTotalItemCount"] = (object?)dto.WoTotalItemCount ?? "",
    };

    /// <summary>
    /// 判断执行量是否达到完成阈值：
    /// total >= planWeight * purchaseCompleteRatio（完成率≥阈值）
    /// 且 total >= planWeight - purchaseCompleteDeviation（绝对偏差≤阈值kg）
    /// </summary>
    internal static bool IsThresholdMet(decimal total, decimal planWeight, decimal purchaseCompleteRatio, decimal purchaseCompleteDeviation)
    {
        return total >= planWeight * purchaseCompleteRatio && total >= planWeight - purchaseCompleteDeviation;
    }
}
