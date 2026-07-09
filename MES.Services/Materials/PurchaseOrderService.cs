using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
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
        // LEFT JOIN WorkOrders，直接投影到 PurchaseOrderDto
        // 所有 Wo* 字段在投影中一并填充，后续 ApplyFilters/ApplySort 直接操作 DTO 属性
        var queryable = from p in _context.PurchaseOrders.AsNoTracking()
                        join w in _context.WorkOrders.AsNoTracking() on p.SourceWorkOrderNo equals w.WorkOrderNo into wj
                        from w in wj.DefaultIfEmpty()
                        select new PurchaseOrderDto
                        {
                            Id = p.Id,
                            OrderNo = p.OrderNo,
                            SupplierId = p.SupplierId,
                            SupplierName = p.SupplierName,
                            OrderDate = p.OrderDate,
                            Status = p.Status,
                            IsForceCompleted = p.IsForceCompleted,
                            MaterialCategory = p.MaterialCategory,
                            PlantGrade = p.PlantGrade,
                            Specification = p.Specification,
                            UnitWeight = p.UnitWeight,
                            Quantity = p.Quantity,
                            Weight = p.Weight,
                            RequiredDate = p.RequiredDate,
                            UnitPrice = p.UnitPrice,
                            TotalAmount = p.TotalAmount,
                            LastArrivalDate = p.LastArrivalDate,
                            ReceivedQuantity = p.ReceivedQuantity,
                            ReceivedWeight = p.ReceivedWeight,
                            SourceWorkOrderNo = p.SourceWorkOrderNo,
                            InputMultiple = p.InputMultiple,
                            Remark = p.Remark,
                            CreatedTime = p.CreatedTime,
                            WoSalesOrderNo = w.SalesOrderNo,
                            WoProductionMainNo = w.ProductionMainNo,
                            WoProductionSubNo = w.ProductionSubNo,
                            WoSignDate = (DateTime?)w.SignDate,
                            WoSalesman = w.Salesman,
                            WoEndCustomer = w.EndCustomer,
                            WoDeliveryDate = (DateTime?)w.DeliveryDate,
                            WoDelayPenalty = w != null && w.DelayPenalty,
                            WoSettlementMethod = (SettlementMethod?)w.SettlementMethod,
                            WoPlantGrade = w.PlantGrade,
                            WoSpecification = w.Specification,
                            WoLengthStatus = (LengthStatus?)w.LengthStatus,
                            WoMaxLength = w.MaxLength,
                            WoTotalQuantity = (int?)w.TotalQuantity,
                            WoTotalWeight = (decimal?)w.TotalWeight,
                            WoDeliveryState = (DeliveryState?)w.DeliveryState,
                            WoTotalItemCount = (int?)w.TotalItemCount,
                        };

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(dto =>
                dto.OrderNo.Contains(kw) ||
                dto.MaterialCategory.Contains(kw) ||
                dto.PlantGrade.Contains(kw) ||
                dto.Specification.Contains(kw) ||
                (dto.SupplierName != null && dto.SupplierName.Contains(kw)) ||
                (dto.SourceWorkOrderNo != null && dto.SourceWorkOrderNo.Contains(kw)) ||
                (dto.WoSalesOrderNo != null && dto.WoSalesOrderNo.Contains(kw)) ||
                (dto.WoProductionMainNo != null && dto.WoProductionMainNo.Contains(kw)) ||
                (dto.WoProductionSubNo != null && dto.WoProductionSubNo.Contains(kw)) ||
                (dto.WoSalesman != null && dto.WoSalesman.Contains(kw)) ||
                (dto.WoEndCustomer != null && dto.WoEndCustomer.Contains(kw)) ||
                (dto.WoPlantGrade != null && dto.WoPlantGrade.Contains(kw)) ||
                (dto.WoSpecification != null && dto.WoSpecification.Contains(kw)) ||
                (dto.Remark != null && dto.Remark.Contains(kw)));
        }

        // 状态筛选
        if (!string.IsNullOrEmpty(query.Status) && Enum.TryParse<PurchaseOrderStatus>(query.Status, out var parsedStatus))
        {
            queryable = queryable.Where(p => p.Status == parsedStatus);
        }

        // 下单日期筛选
        if (query.DateFrom.HasValue)
        {
            var from = query.DateFrom.Value.Date;
            queryable = queryable.Where(p => p.OrderDate >= from);
        }
        if (query.DateTo.HasValue)
        {
            var to = query.DateTo.Value.Date.AddDays(1);
            queryable = queryable.Where(p => p.OrderDate < to);
        }

        // 要求到货日筛选
        if (query.RequiredDateFrom.HasValue)
        {
            var from = query.RequiredDateFrom.Value.Date;
            queryable = queryable.Where(p => p.RequiredDate >= from);
        }
        if (query.RequiredDateTo.HasValue)
        {
            var to = query.RequiredDateTo.Value.Date.AddDays(1);
            queryable = queryable.Where(p => p.RequiredDate < to);
        }

        // ApplyFilters 直接反射操作 DTO 属性，Wo* 字段无需特殊处理
        queryable = queryable.ApplyFilters(query.Filters);

        // ApplySort 直接反射操作 DTO 属性，Wo* 字段无需 GroupJoin
        queryable = queryable.ApplySort(query.SortBy ?? "orderdate", query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<PurchaseOrderDto>
        {
            Items = items,
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
                          select new PurchaseOrderDto
                          {
                              Id = p.Id,
                              OrderNo = p.OrderNo,
                              SupplierId = p.SupplierId,
                              SupplierName = p.SupplierName,
                              OrderDate = p.OrderDate,
                              Status = p.Status,
                              IsForceCompleted = p.IsForceCompleted,
                              MaterialCategory = p.MaterialCategory,
                              PlantGrade = p.PlantGrade,
                              Specification = p.Specification,
                              UnitWeight = p.UnitWeight,
                              Quantity = p.Quantity,
                              Weight = p.Weight,
                              RequiredDate = p.RequiredDate,
                              UnitPrice = p.UnitPrice,
                              TotalAmount = p.TotalAmount,
                              LastArrivalDate = p.LastArrivalDate,
                              ReceivedQuantity = p.ReceivedQuantity,
                              ReceivedWeight = p.ReceivedWeight,
                              SourceWorkOrderNo = p.SourceWorkOrderNo,
                              InputMultiple = p.InputMultiple,
                              Remark = p.Remark,
                              CreatedTime = p.CreatedTime,
                              WoSalesOrderNo = w.SalesOrderNo,
                              WoProductionMainNo = w.ProductionMainNo,
                              WoProductionSubNo = w.ProductionSubNo,
                              WoSignDate = (DateTime?)w.SignDate,
                              WoSalesman = w.Salesman,
                              WoEndCustomer = w.EndCustomer,
                              WoDeliveryDate = (DateTime?)w.DeliveryDate,
                              WoDelayPenalty = w != null && w.DelayPenalty,
                              WoSettlementMethod = (SettlementMethod?)w.SettlementMethod,
                              WoPlantGrade = w.PlantGrade,
                              WoSpecification = w.Specification,
                              WoLengthStatus = (LengthStatus?)w.LengthStatus,
                              WoMaxLength = w.MaxLength,
                              WoTotalQuantity = (int?)w.TotalQuantity,
                              WoTotalWeight = (decimal?)w.TotalWeight,
                              WoDeliveryState = (DeliveryState?)w.DeliveryState,
                              WoTotalItemCount = (int?)w.TotalItemCount,
                          }).ToListAsync();

        return items;
    }

    public async Task<PurchaseOrderDto> GetByIdAsync(int id)
    {
        var dto = await (from p in _context.PurchaseOrders.AsNoTracking()
                         join w in _context.WorkOrders.AsNoTracking() on p.SourceWorkOrderNo equals w.WorkOrderNo into wj
                         from w in wj.DefaultIfEmpty()
                         where p.Id == id
                         select new PurchaseOrderDto
                         {
                             Id = p.Id,
                             OrderNo = p.OrderNo,
                             SupplierId = p.SupplierId,
                             SupplierName = p.SupplierName,
                             OrderDate = p.OrderDate,
                             Status = p.Status,
                             IsForceCompleted = p.IsForceCompleted,
                             MaterialCategory = p.MaterialCategory,
                             PlantGrade = p.PlantGrade,
                             Specification = p.Specification,
                             UnitWeight = p.UnitWeight,
                             Quantity = p.Quantity,
                             Weight = p.Weight,
                             RequiredDate = p.RequiredDate,
                             UnitPrice = p.UnitPrice,
                             TotalAmount = p.TotalAmount,
                             LastArrivalDate = p.LastArrivalDate,
                             ReceivedQuantity = p.ReceivedQuantity,
                             ReceivedWeight = p.ReceivedWeight,
                             SourceWorkOrderNo = p.SourceWorkOrderNo,
                             InputMultiple = p.InputMultiple,
                             Remark = p.Remark,
                             CreatedTime = p.CreatedTime,
                             WoSalesOrderNo = w.SalesOrderNo,
                             WoProductionMainNo = w.ProductionMainNo,
                             WoProductionSubNo = w.ProductionSubNo,
                             WoSignDate = (DateTime?)w.SignDate,
                             WoSalesman = w.Salesman,
                             WoEndCustomer = w.EndCustomer,
                             WoDeliveryDate = (DateTime?)w.DeliveryDate,
                             WoDelayPenalty = w != null && w.DelayPenalty,
                             WoSettlementMethod = (SettlementMethod?)w.SettlementMethod,
                             WoPlantGrade = w.PlantGrade,
                             WoSpecification = w.Specification,
                             WoLengthStatus = (LengthStatus?)w.LengthStatus,
                             WoMaxLength = w.MaxLength,
                             WoTotalQuantity = (int?)w.TotalQuantity,
                             WoTotalWeight = (decimal?)w.TotalWeight,
                             WoDeliveryState = (DeliveryState?)w.DeliveryState,
                             WoTotalItemCount = (int?)w.TotalItemCount,
                         }).FirstOrDefaultAsync();

        if (dto == null) throw new BusinessException("采购单不存在");
        return dto;
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
                MaterialCategory = request.MaterialCategory,
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
        _ = TryRefreshExecutionSummaryAsync(entity.SourceWorkOrderNo);
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
                    MaterialCategory = request.MaterialCategory,
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
        foreach (var woNo in distinctWoNos) _ = TryRefreshExecutionSummaryAsync(woNo);

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
            _ = TryRefreshExecutionSummaryAsync(entity.SourceWorkOrderNo);
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
        _ = TryRefreshExecutionSummaryAsync(entity.SourceWorkOrderNo);
        return dto2;
    }

    private static void MapUpdateFields(PurchaseOrder entity, UpdatePurchaseOrderRequest request)
    {
        entity.SupplierId = request.SupplierId;
        entity.MaterialCategory = request.MaterialCategory;
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
        _ = TryRefreshExecutionSummaryAsync(order.SourceWorkOrderNo);
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
        _ = TryRefreshExecutionSummaryAsync(entity.SourceWorkOrderNo);
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
        _ = TryRefreshExecutionSummaryAsync(deletedWoNo);
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
        SupplierName = entity.SupplierName,
        OrderDate = entity.OrderDate,
        Status = entity.Status,
        IsForceCompleted = entity.IsForceCompleted,
        MaterialCategory = entity.MaterialCategory,
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
                CategoryName = g.Key.RawMaterialType == RawMaterialType.SemiFinished ? "荒管" : "半成品",
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
                CategoryName = g.Key.ProductType == FinishedProductType.Critical ? "临界成品" : "订单成品",
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
                    MaterialCategory = "圆棒穿孔",
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
        if (materialCategory == "荒管" || materialCategory == "半成品")
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
        if (materialCategory == "临界成品" || materialCategory == "订单成品" || materialCategory == "成品")
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
            MaterialCategory = "荒管", // 圆棒穿孔实际消耗的是荒管
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

    public async Task<byte[]> PrintOrderAsync(int id)
    {
        var dto = await GetByIdAsync(id);
        return PurchaseOrderPrintHelper.GeneratePdf(dto);
    }

    public async Task<byte[]> PrintOrderBatchAsync(int[] ids)
    {
        var orders = await GetByIdsAsync(ids);
        return PurchaseOrderPrintHelper.GenerateBatchPdf(orders);
    }

    public async Task<List<PurchaseOrderDto>> GetByIdsAsync(int[] ids)
    {
        var items = await (from p in _context.PurchaseOrders.AsNoTracking()
                          join w in _context.WorkOrders.AsNoTracking() on p.SourceWorkOrderNo equals w.WorkOrderNo into wj
                          from w in wj.DefaultIfEmpty()
                          where ids.Contains(p.Id)
                          select new PurchaseOrderDto
                          {
                              Id = p.Id,
                              OrderNo = p.OrderNo,
                              SupplierId = p.SupplierId,
                              SupplierName = p.SupplierName,
                              OrderDate = p.OrderDate,
                              Status = p.Status,
                              IsForceCompleted = p.IsForceCompleted,
                              MaterialCategory = p.MaterialCategory,
                              PlantGrade = p.PlantGrade,
                              Specification = p.Specification,
                              UnitWeight = p.UnitWeight,
                              Quantity = p.Quantity,
                              Weight = p.Weight,
                              RequiredDate = p.RequiredDate,
                              UnitPrice = p.UnitPrice,
                              TotalAmount = p.TotalAmount,
                              LastArrivalDate = p.LastArrivalDate,
                              ReceivedQuantity = p.ReceivedQuantity,
                              ReceivedWeight = p.ReceivedWeight,
                              SourceWorkOrderNo = p.SourceWorkOrderNo,
                              InputMultiple = p.InputMultiple,
                              Remark = p.Remark,
                              CreatedTime = p.CreatedTime,
                              WoSalesOrderNo = w.SalesOrderNo,
                              WoProductionMainNo = w.ProductionMainNo,
                              WoProductionSubNo = w.ProductionSubNo,
                              WoSignDate = (DateTime?)w.SignDate,
                              WoSalesman = w.Salesman,
                              WoEndCustomer = w.EndCustomer,
                              WoDeliveryDate = (DateTime?)w.DeliveryDate,
                              WoDelayPenalty = w != null && w.DelayPenalty,
                              WoSettlementMethod = (SettlementMethod?)w.SettlementMethod,
                              WoPlantGrade = w.PlantGrade,
                              WoSpecification = w.Specification,
                              WoLengthStatus = (LengthStatus?)w.LengthStatus,
                              WoMaxLength = w.MaxLength,
                              WoTotalQuantity = (int?)w.TotalQuantity,
                              WoTotalWeight = (decimal?)w.TotalWeight,
                              WoDeliveryState = (DeliveryState?)w.DeliveryState,
                              WoTotalItemCount = (int?)w.TotalItemCount,
                          }).ToListAsync();

        return items;
    }

    public async Task<byte[]> PrintOrderAllAsync(string? keyword, string? sortBy = null, bool isDescending = false)
    {
        var query = new PurchaseOrderQueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy ?? "CreatedTime",
            IsDescending = isDescending
        };
        var paged = await GetPagedAsync(query);
        return PurchaseOrderPrintHelper.GenerateBatchPdf(paged.Items);
    }

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
