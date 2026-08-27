using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Auth;
using MES.Core.Constants;
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

    // 空值筛选哨兵（与前端 ExcelFilter/BatchPlans 的 "__EXCEL_FILTER_NULL__" 一致）
    private const string FilterNull = "__EXCEL_FILTER_NULL__";

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

    /// <summary>
    /// 来源工单号变更后新旧都刷新，避免旧工单读模型残留
    /// </summary>
    private async Task TryRefreshExecutionSummaryBothAsync(string? oldSourceWorkOrderNo, string? newSourceWorkOrderNo)
    {
        var nos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(oldSourceWorkOrderNo)) nos.Add(oldSourceWorkOrderNo);
        if (!string.IsNullOrWhiteSpace(newSourceWorkOrderNo)) nos.Add(newSourceWorkOrderNo);
        foreach (var no in nos)
            await TryRefreshExecutionSummaryAsync(no);
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

    /// <summary>
    /// 按采购单号汇总退货量（仅统计退货出库 ReturnOut 的出库支数/重量）。
    /// 关联链：采购单号 → 仓库批（InventoryBatch.SourceOrderNo）→ 退货出库记录（OutboundRecord.InventoryBatchId）。
    /// </summary>
    private async Task<Dictionary<string, (int Quantity, decimal Weight)>> BuildReturnSummaryAsync(IReadOnlyCollection<string> orderNos)
    {
        var result = new Dictionary<string, (int, decimal)>(StringComparer.OrdinalIgnoreCase);
        if (orderNos.Count == 0) return result;
        foreach (var no in orderNos.Distinct(StringComparer.OrdinalIgnoreCase))
            result[no] = (0, 0m);

        // 按采购单号查其采购入库的仓库批，建立「原仓库批批次号 → 采购单号」映射
        var batches = await _context.InventoryBatches.AsNoTracking()
            .Where(b => b.SourceOrderNo != null && orderNos.Contains(b.SourceOrderNo))
            .Select(b => new { b.BatchNo, b.SourceOrderNo })
            .ToListAsync();

        var batchNoToOrderNo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in batches.Where(b => !string.IsNullOrEmpty(b.BatchNo) && !string.IsNullOrEmpty(b.SourceOrderNo)))
            batchNoToOrderNo[b.BatchNo!] = b.SourceOrderNo!;
        if (batchNoToOrderNo.Count == 0) return result;

        // 退货出库以「退货-原仓库批（ReturnSourceBatchNo=原仓库批批次号）」反查原仓库批再关联采购单
        var batchNos = batchNoToOrderNo.Keys.ToList();
        foreach (var chunk in batchNos.Chunk(1000))
        {
            var outbounds = await _context.OutboundRecords.AsNoTracking()
                .Where(o => o.OutboundType == OutboundType.ReturnOut
                         && o.ReturnSourceBatchNo != null
                         && chunk.Contains(o.ReturnSourceBatchNo))
                .ToListAsync();
            foreach (var o in outbounds)
            {
                if (!batchNoToOrderNo.TryGetValue(o.ReturnSourceBatchNo!, out var orderNo)) continue;
                var (q, w) = result[orderNo];
                result[orderNo] = (q + o.OutboundQuantity, w + o.OutboundWeight);
            }
        }
        return result;
    }

    public async Task<PagedResult<PurchaseOrderDto>> GetPagedAsync(PurchaseOrderQueryParams query)
    {
        // 实体级查询（MaterialCategory 为字符串，用于 DB 端筛选和排序）
        var entityQuery = from p in _context.PurchaseOrders.AsNoTracking()
                          join w in _context.WorkOrders.AsNoTracking() on p.SourceWorkOrderNo equals w.WorkOrderNo into wj
                          from w in wj.DefaultIfEmpty()
                          join e in _context.WorkOrderExecutionSummaries.AsNoTracking() on p.SourceWorkOrderNo equals e.WorkOrderNo into ej
                          from e in ej.DefaultIfEmpty()
                          select new { p, w, e, MaterialCategory = p.MaterialCategory };

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
            x.p.CreatedBy, x.p.UpdatedBy, x.p.UpdatedTime,
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
            ExecutionScheduleStage = x.e != null ? (int?)x.e.ScheduleStage : null,
            ExecutionUrgencyLevel = x.e != null ? x.e.UrgencyLevel : null,
            ExecutionRawMaterialLockRemark = x.e != null ? x.e.RawMaterialLockRemark : null,
            ExecutionTheoreticalCutoffDate = x.e != null ? (DateTime?)x.e.TheoreticalCutoffDate : null,
        });

        // 通用筛选
        dtoQuery = dtoQuery.ApplyFilters(query.Filters);

        var totalCount = await dtoQuery.CountAsync();

        // 排序：按 SortKey 反射属性名排序，覆盖全部列（含来源销售订单 G3 隐藏列）
        dtoQuery = dtoQuery.ApplySort(query.SortBy ?? "orderdate", query.IsDescending);

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
            CreatedBy = x.CreatedBy,
            CreatedTime = x.CreatedTime,
            UpdatedBy = x.UpdatedBy,
            UpdatedTime = x.UpdatedTime,
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
            ExecutionScheduleStage = x.ExecutionScheduleStage,
            ExecutionUrgencyLevel = x.ExecutionUrgencyLevel,
            ExecutionRawMaterialLockRemark = x.ExecutionRawMaterialLockRemark,
            ExecutionTheoreticalCutoffDate = x.ExecutionTheoreticalCutoffDate,
        }).ToList();

        // 退货量（内存补充：按采购单号汇总退货出库 ReturnOut 支数/重量）
        var returnMap = await BuildReturnSummaryAsync(dtos.Select(d => d.OrderNo).ToList());
        foreach (var d in dtos)
        {
            var (rq, rw) = returnMap[d.OrderNo];
            d.ReturnQuantity = rq;
            d.ReturnWeight = rw;
        }

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
                           join e in _context.WorkOrderExecutionSummaries.AsNoTracking() on p.SourceWorkOrderNo equals e.WorkOrderNo into ej
                           from e in ej.DefaultIfEmpty()
                           orderby p.OrderDate, p.OrderNo
                           select new
                           {
                               p, w, e, MaterialCategory = p.MaterialCategory
                           }).ToListAsync();

        var dtos = items.Select(x => new PurchaseOrderDto
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
            CreatedBy = x.p.CreatedBy,
            CreatedTime = x.p.CreatedTime,
            UpdatedBy = x.p.UpdatedBy,
            UpdatedTime = x.p.UpdatedTime,
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
            ExecutionScheduleStage = x.e != null ? (int?)x.e.ScheduleStage : null,
            ExecutionUrgencyLevel = x.e != null ? x.e.UrgencyLevel : null,
            ExecutionRawMaterialLockRemark = x.e != null ? x.e.RawMaterialLockRemark : null,
            ExecutionTheoreticalCutoffDate = x.e != null ? (DateTime?)x.e.TheoreticalCutoffDate : null,
        }).ToList();

        // 退货量（内存补充）
        var returnMap = await BuildReturnSummaryAsync(dtos.Select(d => d.OrderNo).ToList());
        foreach (var d in dtos)
        {
            var (rq, rw) = returnMap[d.OrderNo];
            d.ReturnQuantity = rq;
            d.ReturnWeight = rw;
        }
        return dtos;
    }

    public async Task<PurchaseOrderDto> GetByIdAsync(int id)
    {
        var item = await (from p in _context.PurchaseOrders.AsNoTracking()
                          join w in _context.WorkOrders.AsNoTracking() on p.SourceWorkOrderNo equals w.WorkOrderNo into wj
                          from w in wj.DefaultIfEmpty()
                          join e in _context.WorkOrderExecutionSummaries.AsNoTracking() on p.SourceWorkOrderNo equals e.WorkOrderNo into ej
                          from e in ej.DefaultIfEmpty()
                          where p.Id == id
                          select new { p, w, e, MaterialCategory = p.MaterialCategory }).FirstOrDefaultAsync();

        if (item == null) throw new BusinessException("采购单不存在");

        var dto = new PurchaseOrderDto
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
            CreatedBy = item.p.CreatedBy,
            CreatedTime = item.p.CreatedTime,
            UpdatedBy = item.p.UpdatedBy,
            UpdatedTime = item.p.UpdatedTime,
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
            ExecutionScheduleStage = item.e != null ? (int?)item.e.ScheduleStage : null,
            ExecutionUrgencyLevel = item.e != null ? item.e.UrgencyLevel : null,
            ExecutionRawMaterialLockRemark = item.e != null ? item.e.RawMaterialLockRemark : null,
            ExecutionTheoreticalCutoffDate = item.e != null ? (DateTime?)item.e.TheoreticalCutoffDate : null,
        };

        var returnMap = await BuildReturnSummaryAsync(new[] { dto.OrderNo });
        var (rq, rw) = returnMap[dto.OrderNo];
        dto.ReturnQuantity = rq;
        dto.ReturnWeight = rw;
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

        // 来源工单号变更时，旧工单读模型也需刷新（G4~G10 可用余量等）
        var oldSourceWorkOrderNo = entity.SourceWorkOrderNo;

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
            await TryRefreshExecutionSummaryBothAsync(oldSourceWorkOrderNo, entity.SourceWorkOrderNo);
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

            // 非强制完成时自动计算状态（净到货 = 已到货 - 退货）
            if (!entity.IsForceCompleted)
            {
                var ratio = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteRatio", 0.965m);
                var deviation = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteDeviation", 200m);
                var overRatio = await GetConfigAsync("WarehouseThreshold", "PurchaseOverRatio", 1.05m);
                var overDeviation = await GetConfigAsync("WarehouseThreshold", "PurchaseOverDeviation", 100m);
                var retW = (await BuildReturnSummaryAsync(new[] { entity.OrderNo })).GetValueOrDefault(entity.OrderNo).Weight;
                RecalcPurchaseStatus(entity, ratio, deviation, overRatio, overDeviation, retW);
            }
        }

        await _context.SaveChangesAsync();

        var dto2 = ToDto(entity);
        await TryRefreshExecutionSummaryBothAsync(oldSourceWorkOrderNo, entity.SourceWorkOrderNo);
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

        // 退货量（按采购单号，一次性查询避免 N+1）
        var returnSummary = await BuildReturnSummaryAsync(orderNos);

        foreach (var order in orders)
        {
            var orderBatches = batches.Where(b => string.Equals(b.SourceOrderNo, order.OrderNo, StringComparison.OrdinalIgnoreCase)).ToList();

            // 关联批次可能已删光（无匹配）→ 到货字段回退为 0，避免残留快照（空批次 Sum=0，Max 需判空）
            order.ReceivedQuantity = orderBatches.Sum(b => b.InitialQuantity);
            order.ReceivedWeight = orderBatches.Sum(b => b.InitialWeight);
            order.LastArrivalDate = orderBatches.Count > 0 ? orderBatches.Max(b => b.InboundDate) : null;

            if (!order.IsForceCompleted)
            {
                var ratio = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteRatio", 0.965m);
                var deviation = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteDeviation", 200m);
                var overRatio = await GetConfigAsync("WarehouseThreshold", "PurchaseOverRatio", 1.05m);
                var overDeviation = await GetConfigAsync("WarehouseThreshold", "PurchaseOverDeviation", 100m);
                RecalcPurchaseStatus(order, ratio, deviation, overRatio, overDeviation, returnSummary.GetValueOrDefault(order.OrderNo).Weight);
            }
        }

        await _context.SaveChangesAsync();

        // 去重刷新关联工单的执行状况
        foreach (var woNo in orders.Where(o => !string.IsNullOrWhiteSpace(o.SourceWorkOrderNo))
                         .Select(o => o.SourceWorkOrderNo)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            await TryRefreshExecutionSummaryAsync(woNo!);
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
            var overRatio = await GetConfigAsync("WarehouseThreshold", "PurchaseOverRatio", 1.05m);
            var overDeviation = await GetConfigAsync("WarehouseThreshold", "PurchaseOverDeviation", 100m);
            var retW = (await BuildReturnSummaryAsync(new[] { order.OrderNo })).GetValueOrDefault(order.OrderNo).Weight;
            RecalcPurchaseStatus(order, ratio, deviation, overRatio, overDeviation, retW);
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
            var overRatio = await GetConfigAsync("WarehouseThreshold", "PurchaseOverRatio", 1.05m);
            var overDeviation = await GetConfigAsync("WarehouseThreshold", "PurchaseOverDeviation", 100m);
            var retW = (await BuildReturnSummaryAsync(new[] { entity.OrderNo })).GetValueOrDefault(entity.OrderNo).Weight;
            RecalcPurchaseStatus(entity, ratio, deviation, overRatio, overDeviation, retW);
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

    private static void RecalcPurchaseStatus(PurchaseOrder order, decimal purchaseCompleteRatio, decimal purchaseCompleteDeviation, decimal purchaseOverRatio, decimal purchaseOverDeviation, decimal returnWeight)
    {
        // 净到货量 = 已到货量（多次到货累加）- 退货量（不合格退回回冲），以净到货对比采购量确定状态
        var received = Math.Max(0, order.ReceivedWeight - returnWeight);
        if (received == 0)
            order.Status = PurchaseOrderStatus.Open;
        else if (received > order.Weight * purchaseOverRatio
                 && received - order.Weight > purchaseOverDeviation)
            order.Status = PurchaseOrderStatus.OverReceived;
        else if (IsThresholdMet(received, order.Weight, purchaseCompleteRatio, purchaseCompleteDeviation))
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
        CreatedBy = entity.CreatedBy,
        CreatedTime = entity.CreatedTime,
        UpdatedBy = entity.UpdatedBy,
        UpdatedTime = entity.UpdatedTime
    };

    public async Task<List<ProcurementStatusDto>> GetProcurementStatusAsync()
    {
        var purchaseCompleteRatio = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteRatio", 0.965m);
        var purchaseCompleteDeviation = await GetConfigAsync("WarehouseThreshold", "PurchaseCompleteDeviation", 200m);
        var purchaseOverRatio = await GetConfigAsync("WarehouseThreshold", "PurchaseOverRatio", 1.05m);

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

        // 3. 原料采购计划：按工单号+原料类型汇总（工厂牌号取计划行级，同组多牌号去重）
        var semiPlanData = await _context.PurchaseSemiPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0 && workOrderIds.Contains(p.WorkOrderId))
            .GroupBy(p => new { p.WorkOrderId, p.RawMaterialType })
            .Select(g => new
            {
                g.Key.WorkOrderId,
                CategoryName = g.Key.RawMaterialType == MaterialType.RoughTube ? InventoryMaterialTypes.RoughTube : InventoryMaterialTypes.SemiFinished,
                PlanWeight = g.Sum(p => p.RequiredWeight),
                PlantGrades = g.Select(p => p.PlantGrade).Distinct().ToList()
            })
            .ToListAsync();

        // 4. 成品采购计划：按工单号+成品类型汇总（工厂牌号取计划行级，同组多牌号去重）
        var finishedPlanData = await _context.PurchaseFinishedPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0 && workOrderIds.Contains(p.WorkOrderId))
            .GroupBy(p => new { p.WorkOrderId, p.ProductType })
            .Select(g => new
            {
                g.Key.WorkOrderId,
                CategoryName = g.Key.ProductType == FinishedProductType.Critical ? InventoryMaterialTypes.CriticalFinished
                    : g.Key.ProductType == FinishedProductType.SpecialDeliveryStatus ? InventoryMaterialTypes.SpecialDeliveryStatus
                    : InventoryMaterialTypes.OrderFinished,
                PlanWeight = g.Sum(p => p.RequiredWeight),
                PlantGrades = g.Select(p => p.PlantGrade).Distinct().ToList()
            })
            .ToListAsync();

        // 5. 按工单号+物料分类聚合已采购重量（与计划分组对齐，避免同工单多类计划串量）
        var purchaseWeights = new Dictionary<string, decimal>();
        if (allWorkOrderNos.Count > 0)
        {
            var purchaseData = await _context.PurchaseOrders
                .AsNoTracking()
                .Where(p => p.SourceWorkOrderNo != null && allWorkOrderNos.Contains(p.SourceWorkOrderNo))
                .GroupBy(p => new { p.SourceWorkOrderNo, p.MaterialCategory })
                .Select(g => new { g.Key.SourceWorkOrderNo, g.Key.MaterialCategory, Weight = g.Sum(p => p.Weight) })
                .ToListAsync();
            purchaseWeights = purchaseData
                .ToDictionary(x => $"{x.SourceWorkOrderNo}|{x.MaterialCategory}", x => x.Weight, StringComparer.OrdinalIgnoreCase);
        }

        // 6. 按工单号关联工单执行状况读模型（工单关注/原锁执行/工单计划性）
        var execMap = await BuildExecutionMapAsync(allWorkOrderNos);

        // 7. 合并原料+成品计划数据，计算采购执行状态（执行量=已采购，委外穿孔不属采购计划）
        var allPlanData = semiPlanData.Concat(finishedPlanData)
            .Select(x =>
            {
                var workOrderNo = workOrders.GetValueOrDefault(x.WorkOrderId, "");
                var purchaseW = purchaseWeights.GetValueOrDefault($"{workOrderNo}|{x.CategoryName}", 0);
                var (execStage, execUrgency, execLock) = execMap.GetValueOrDefault(workOrderNo);
                return new ProcurementStatusDto
                {
                    WorkOrderNo = workOrderNo,
                    MaterialName = workOrderNo,
                    MaterialCategory = EnumHelper.TryParse<MaterialType>(x.CategoryName),
                    PlantGrade = x.PlantGrades.Count > 0 ? string.Join("、", x.PlantGrades) : null,
                    PlanWeight = x.PlanWeight,
                    PurchaseWeight = purchaseW,
                    MissingWeight = Math.Max(0, x.PlanWeight - purchaseW),
                    ExecutionScheduleStage = execStage,
                    ExecutionUrgencyLevel = execUrgency,
                    ExecutionRawMaterialLockRemark = execLock,
                    StatusText = purchaseW == 0 ? "未采购"
                        : purchaseW > x.PlanWeight * purchaseOverRatio ? "超额采购"
                        : IsThresholdMet(purchaseW, x.PlanWeight, purchaseCompleteRatio, purchaseCompleteDeviation) ? "已采购"
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
        var purchaseOverRatio = await GetConfigAsync("WarehouseThreshold", "PurchaseOverRatio", 1.05m);

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

        // 3. 圆棒穿孔计划：按工单号汇总（工厂牌号取计划行级，同工单多牌号去重）
        var piercingPlanData = await _context.RoundBarPiercingPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0 && piercingWorkOrderIds.Contains(p.WorkOrderId))
            .GroupBy(p => p.WorkOrderId)
            .Select(g => new
            {
                WorkOrderId = g.Key,
                PlanWeight = g.Sum(p => p.RequiredWeight),
                PlantGrades = g.Select(p => p.PlantGrade).Distinct().ToList()
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

        // 5. 按工单号关联工单执行状况读模型（工单关注/原锁执行/工单计划性）
        var execMap = await BuildExecutionMapAsync(allWorkOrderNos);

        // 6. 合并数据，计算执行状态（执行量=已委外，缺少量=计划-已委外）
        var result = piercingPlanData
            .Select(x =>
            {
                var workOrderNo = workOrders.GetValueOrDefault(x.WorkOrderId, "");
                var subW = subcontractWeights.GetValueOrDefault(workOrderNo, 0);
                var (execStage, execUrgency, execLock) = execMap.GetValueOrDefault(workOrderNo);
                return new ProcurementStatusDto
                {
                    WorkOrderNo = workOrderNo,
                    MaterialName = workOrderNo,
                    MaterialCategory = EnumHelper.TryParse<MaterialType>(InventoryMaterialTypes.RoundBar),
                    PlantGrade = x.PlantGrades.Count > 0 ? string.Join("、", x.PlantGrades) : null,
                    PlanWeight = x.PlanWeight,
                    PurchaseWeight = 0,
                    SubcontractWeight = subW,
                    MissingWeight = Math.Max(0, x.PlanWeight - subW),
                    ExecutionScheduleStage = execStage,
                    ExecutionUrgencyLevel = execUrgency,
                    ExecutionRawMaterialLockRemark = execLock,
                    StatusText = subW == 0 ? "未穿孔"
                        : subW > x.PlanWeight * purchaseOverRatio ? "超额穿孔"
                        : IsThresholdMet(subW, x.PlanWeight, purchaseCompleteRatio, purchaseCompleteDeviation) ? "已穿孔"
                        : "部分穿孔"
                };
            })
            .Where(x => x.StatusText != "已穿孔" && !string.IsNullOrEmpty(x.WorkOrderNo))
            .OrderBy(x => x.WorkOrderNo)
            .ToList();

        return result;
    }

    /// <summary>
    /// 按工单号集合构建工单执行状况读模型映射（工单关注/原锁执行/工单计划性），无记录返回 null
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

    // ========== 采购首页汇总（荒管/成品） ==========

    // 荒管物料分类（采购单 MaterialCategory 存储值）
    private static readonly string[] SemiCategories = { InventoryMaterialTypes.RoughTube, InventoryMaterialTypes.SemiFinished };

    // 成品物料分类（采购单 MaterialCategory 存储值 = MaterialType 枚举名）
    private static readonly string[] FinishedCategories = { InventoryMaterialTypes.CriticalFinished, InventoryMaterialTypes.OrderFinished, InventoryMaterialTypes.SpecialDeliveryStatus };

    private static HashSet<string> GetCategorySet(bool isFinished)
        => new(isFinished ? FinishedCategories : SemiCategories, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 待购实时数据（荒管/成品）：按（工单号+物料分类）聚合，待购量=Max(0, 计划总量-已采购量)；
    /// 厂内钢种/规格=组内计划多值合并；工单关注/原锁执行/工单计划性按工单号关联读模型（无记录 null）
    /// </summary>
    public async Task<List<PurchasePendingDto>> GetPurchasePendingAsync(bool isFinished)
    {
        var categorySet = GetCategorySet(isFinished);

        // 1. 计划明细（荒管 PurchaseSemiPlans / 成品 PurchaseFinishedPlans）
        List<(int WorkOrderId, string CategoryName, string PlantGrade, string Spec, decimal PlanWeight)> planRows;
        if (isFinished)
        {
            var rows = await _context.PurchaseFinishedPlans.AsNoTracking()
                .Where(p => p.RequiredWeight > 0)
                .Select(p => new { p.WorkOrderId, p.ProductType, p.PlantGrade, p.Specification, p.RequiredWeight })
                .ToListAsync();
            planRows = rows.Select(p => (
                p.WorkOrderId,
                CategoryName: p.ProductType == FinishedProductType.Critical ? InventoryMaterialTypes.CriticalFinished
                    : p.ProductType == FinishedProductType.SpecialDeliveryStatus ? InventoryMaterialTypes.SpecialDeliveryStatus
                    : InventoryMaterialTypes.OrderFinished,
                p.PlantGrade, p.Specification, p.RequiredWeight)).ToList();
        }
        else
        {
            var rows = await _context.PurchaseSemiPlans.AsNoTracking()
                .Where(p => p.RequiredWeight > 0)
                .Select(p => new { p.WorkOrderId, p.RawMaterialType, p.PlantGrade, Spec = p.RawMaterialSpec, p.RequiredWeight })
                .ToListAsync();
            planRows = rows.Select(p => (
                p.WorkOrderId,
                CategoryName: p.RawMaterialType == MaterialType.RoughTube ? InventoryMaterialTypes.RoughTube : InventoryMaterialTypes.SemiFinished,
                p.PlantGrade, p.Spec, p.RequiredWeight)).ToList();
        }
        if (planRows.Count == 0)
            return new List<PurchasePendingDto>();

        // 2. 工单号映射
        var woIds = planRows.Select(p => p.WorkOrderId).Distinct().ToList();
        var workOrders = await _context.WorkOrders.AsNoTracking()
            .Where(w => woIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.WorkOrderNo);
        var allWorkOrderNos = workOrders.Values.ToList();

        // 3. 已采购按（工单号+物料分类）聚合
        var purchaseWeights = new Dictionary<string, decimal>();
        if (allWorkOrderNos.Count > 0)
        {
            var purchaseData = await _context.PurchaseOrders.AsNoTracking()
                .Where(p => p.SourceWorkOrderNo != null && allWorkOrderNos.Contains(p.SourceWorkOrderNo)
                         && categorySet.Contains(p.MaterialCategory))
                .GroupBy(p => new { p.SourceWorkOrderNo, p.MaterialCategory })
                .Select(g => new { g.Key.SourceWorkOrderNo, g.Key.MaterialCategory, Weight = g.Sum(p => p.Weight) })
                .ToListAsync();
            purchaseWeights = purchaseData
                .ToDictionary(x => $"{x.SourceWorkOrderNo}|{x.MaterialCategory}", x => x.Weight, StringComparer.OrdinalIgnoreCase);
        }

        // 4. 工单执行读模型
        var execMap = await BuildExecutionMapAsync(allWorkOrderNos);

        // 5. 按（工单号+物料分类）分组聚合
        return planRows
            .GroupBy(x => new { x.WorkOrderId, x.CategoryName })
            .Select(g =>
            {
                var workOrderNo = workOrders.GetValueOrDefault(g.Key.WorkOrderId, "");
                var purchaseW = purchaseWeights.GetValueOrDefault($"{workOrderNo}|{g.Key.CategoryName}", 0);
                var (execStage, execUrgency, execLock) = execMap.GetValueOrDefault(workOrderNo);
                return new PurchasePendingDto
                {
                    WorkOrderNo = workOrderNo,
                    MaterialCategory = EnumHelper.TryParse<MaterialType>(g.Key.CategoryName),
                    PlantGrade = string.Join(",", g.Select(x => x.PlantGrade).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)),
                    Specification = string.Join(",", g.Select(x => x.Spec).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)),
                    PendingWeight = Math.Max(0, g.Sum(x => x.PlanWeight) - purchaseW),
                    ExecutionScheduleStage = execStage,
                    ExecutionUrgencyLevel = execUrgency,
                    ExecutionRawMaterialLockRemark = execLock
                };
            })
            .Where(x => x.PendingWeight > 0 && !string.IsNullOrEmpty(x.WorkOrderNo))
            .OrderBy(x => x.WorkOrderNo)
            .ThenBy(x => x.MaterialCategory)
            .ToList();
    }

    /// <summary>
    /// 在购实时数据（荒管/成品）：状态=已下单+部分到货的采购单，按（供应商×厂内钢种）二维聚合；
    /// 单元格值=采购重量+退货量-已到货量，急量=计划性 A+急/A急 的在购量；含合计行
    /// </summary>
    public async Task<PurchaseInProgressResultDto> GetPurchaseInProgressAsync(bool isFinished)
    {
        var result = new PurchaseInProgressResultDto();
        var categorySet = GetCategorySet(isFinished);
        var statuses = new[] { PurchaseOrderStatus.Open, PurchaseOrderStatus.Partial };

        // 1. 在购采购单（已下单+部分到货）
        var orders = await _context.PurchaseOrders.AsNoTracking()
            .Where(p => statuses.Contains(p.Status) && categorySet.Contains(p.MaterialCategory))
            .Select(p => new { p.OrderNo, p.SupplierName, p.PlantGrade, p.Weight, p.ReceivedWeight, p.SourceWorkOrderNo })
            .ToListAsync();
        if (orders.Count == 0)
            return result;

        // 2. 退货量（按采购单号）
        var returnSummary = await BuildReturnSummaryAsync(orders.Select(o => o.OrderNo).ToList());

        // 3. 急单（计划性 A+急/A急）：按工单号关联读模型
        var urgentSet = new HashSet<string>(new[] { UrgencyLevelKeys.APlusUrgent, UrgencyLevelKeys.AUrgent }, StringComparer.OrdinalIgnoreCase);
        var sourceNos = orders.Select(o => o.SourceWorkOrderNo).Where(n => !string.IsNullOrEmpty(n)).Cast<string>().Distinct().ToList();
        var execMap = await BuildExecutionMapAsync(sourceNos);

        // 4. 按（供应商+厂内钢种）聚合：总量=采购重量+退货量-已到货量，急量=急单的总量
        var cellMap = new Dictionary<string, Dictionary<string, (decimal Total, decimal Urgent)>>(StringComparer.OrdinalIgnoreCase);
        var totalMap = new Dictionary<string, (decimal Total, decimal Urgent)>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in orders)
        {
            var supplier = string.IsNullOrWhiteSpace(o.SupplierName) ? "未填写供应商" : o.SupplierName.Trim();
            var grade = string.IsNullOrWhiteSpace(o.PlantGrade) ? "" : o.PlantGrade.Trim();
            var retW = returnSummary.GetValueOrDefault(o.OrderNo).Weight;
            var inProgress = o.Weight + retW - o.ReceivedWeight;
            if (inProgress <= 0)
                continue; // 已到完/超收，不计在购

            var isUrgent = false;
            if (!string.IsNullOrEmpty(o.SourceWorkOrderNo))
            {
                var (_, urgency, _) = execMap.GetValueOrDefault(o.SourceWorkOrderNo!);
                isUrgent = urgency != null && urgentSet.Contains(urgency);
            }

            if (!cellMap.TryGetValue(supplier, out var supplierCells))
            {
                supplierCells = new Dictionary<string, (decimal, decimal)>(StringComparer.OrdinalIgnoreCase);
                cellMap[supplier] = supplierCells;
            }
            var (t, u) = supplierCells.GetValueOrDefault(grade);
            supplierCells[grade] = (t + inProgress, u + (isUrgent ? inProgress : 0));

            var (st, su) = totalMap.GetValueOrDefault(supplier);
            totalMap[supplier] = (st + inProgress, su + (isUrgent ? inProgress : 0));
        }

        // 5. 构建二维结果 + 合计行
        var steelGrades = cellMap.Values
            .SelectMany(d => d.Keys)
            .Where(g => !string.IsNullOrEmpty(g))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        result.SteelGrades = steelGrades;

        foreach (var kvp in cellMap.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var cells = new Dictionary<string, PurchaseInProgressCellDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in steelGrades)
            {
                var (t, u) = kvp.Value.GetValueOrDefault(g);
                cells[g] = new PurchaseInProgressCellDto { TotalWeight = t, UrgentWeight = u };
            }
            var (gt, gu) = totalMap.GetValueOrDefault(kvp.Key);
            result.Rows.Add(new PurchaseInProgressRowDto
            {
                SupplierName = kvp.Key,
                Cells = cells,
                Total = new PurchaseInProgressCellDto { TotalWeight = gt, UrgentWeight = gu }
            });
        }

        // 合计行
        var grandTotal = new PurchaseInProgressCellDto
        {
            TotalWeight = result.Rows.Sum(r => r.Total.TotalWeight),
            UrgentWeight = result.Rows.Sum(r => r.Total.UrgentWeight)
        };
        var gradeTotals = steelGrades.ToDictionary(
            g => g,
            g => new PurchaseInProgressCellDto
            {
                TotalWeight = result.Rows.Sum(r => r.Cells.GetValueOrDefault(g)?.TotalWeight ?? 0),
                UrgentWeight = result.Rows.Sum(r => r.Cells.GetValueOrDefault(g)?.UrgentWeight ?? 0)
            },
            StringComparer.OrdinalIgnoreCase);
        result.Rows.Add(new PurchaseInProgressRowDto
        {
            SupplierName = "合计",
            Cells = gradeTotals,
            Total = grandTotal
        });

        return result;
    }

    /// <summary>
    /// 月度采购数据（荒管/成品）：按下单日期分月（本年1月~12月），按供应商聚合；
    /// 单元格格式「购X/回Y」，购=该月下单采购重量，回=已到货量-退货量；合计列=12月购/回各自求和；
    /// 现在购=状态已下单+部分到货的 采购重量+退货量-已到货量（不分厂内钢种）；含合计行
    /// </summary>
    public async Task<PurchaseMonthlyResultDto> GetPurchaseMonthlyAsync(bool isFinished)
    {
        var result = new PurchaseMonthlyResultDto();
        var categorySet = GetCategorySet(isFinished);
        var year = DateTime.Today.Year;
        var labels = Enumerable.Range(1, 12).Select(m => $"{year}-{m:00}").ToList();
        result.MonthLabels = labels;

        // 1. 本年采购单（所有状态，含已完成）
        var orders = await _context.PurchaseOrders.AsNoTracking()
            .Where(p => p.OrderDate.Year == year && categorySet.Contains(p.MaterialCategory))
            .Select(p => new { p.OrderNo, p.SupplierName, p.OrderDate, p.Weight, p.ReceivedWeight, p.Status })
            .ToListAsync();
        if (orders.Count == 0)
        {
            // 无本年订单时仅保留全 0 合计行，前端仍可渲染表结构
            result.Rows = new List<PurchaseMonthlyRowDto>
            {
                new() { SupplierName = "合计", Months = labels.Select(_ => new PurchaseMonthlyValueDto()).ToList() }
            };
            return result;
        }

        // 2. 退货量（按采购单号）
        var returnSummary = await BuildReturnSummaryAsync(orders.Select(o => o.OrderNo).ToList());

        // 3. 按供应商聚合（12月购/回 + 合计 + 现在购）
        var rowMap = new Dictionary<string, PurchaseMonthlyRowDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in orders)
        {
            var supplier = string.IsNullOrWhiteSpace(o.SupplierName) ? "未填写供应商" : o.SupplierName.Trim();
            if (!rowMap.TryGetValue(supplier, out var row))
            {
                row = new PurchaseMonthlyRowDto
                {
                    SupplierName = supplier,
                    Months = labels.Select(_ => new PurchaseMonthlyValueDto()).ToList()
                };
                rowMap[supplier] = row;
            }

            var monthIdx = o.OrderDate.Month - 1;
            var retW = returnSummary.GetValueOrDefault(o.OrderNo).Weight;
            var buy = o.Weight;
            var ret = Math.Max(0, o.ReceivedWeight - retW);
            row.Months[monthIdx].BuyWeight += buy;
            row.Months[monthIdx].ReturnWeight += ret;
            row.Total.BuyWeight += buy;
            row.Total.ReturnWeight += ret;

            if (o.Status is PurchaseOrderStatus.Open or PurchaseOrderStatus.Partial)
            {
                var inProgress = o.Weight + retW - o.ReceivedWeight;
                if (inProgress > 0)
                    row.NowInProgress += inProgress;
            }
        }

        result.Rows = rowMap.Values.OrderBy(x => x.SupplierName, StringComparer.OrdinalIgnoreCase).ToList();

        // 4. 合计行
        var totalRow = new PurchaseMonthlyRowDto
        {
            SupplierName = "合计",
            Months = labels.Select(_ => new PurchaseMonthlyValueDto()).ToList()
        };
        foreach (var r in result.Rows)
        {
            for (var i = 0; i < labels.Count; i++)
            {
                totalRow.Months[i].BuyWeight += r.Months[i].BuyWeight;
                totalRow.Months[i].ReturnWeight += r.Months[i].ReturnWeight;
            }
            totalRow.Total.BuyWeight += r.Total.BuyWeight;
            totalRow.Total.ReturnWeight += r.Total.ReturnWeight;
            totalRow.NowInProgress += r.NowInProgress;
        }
        result.Rows.Add(totalRow);

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
        if (materialCategory == InventoryMaterialTypes.RoughTube || materialCategory == InventoryMaterialTypes.SemiFinished)
        {
            var semiPlan = await _context.PurchaseSemiPlans
                .AsNoTracking()
                .Where(p => p.WorkOrderId == workOrder.Id && p.RequiredWeight > 0)
                .FirstOrDefaultAsync();
            if (semiPlan == null) return null;

            return new PlanDetailDto
            {
                WorkOrderNo = workOrderNo,
                MaterialCategory = EnumHelper.TryParse<MaterialType>(materialCategory),
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

        // 成品采购（临界成品/订单成品/订成-非交付态）
        if (materialCategory == InventoryMaterialTypes.CriticalFinished || materialCategory == InventoryMaterialTypes.OrderFinished || materialCategory == InventoryMaterialTypes.SpecialDeliveryStatus)
        {
            var finishedPlan = await _context.PurchaseFinishedPlans
                .AsNoTracking()
                .Where(p => p.WorkOrderId == workOrder.Id && p.RequiredWeight > 0)
                .FirstOrDefaultAsync();
            if (finishedPlan == null) return null;

            return new PlanDetailDto
            {
                WorkOrderNo = workOrderNo,
                MaterialCategory = EnumHelper.TryParse<MaterialType>(materialCategory),
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
            MaterialCategory = EnumHelper.TryParse<MaterialType>(InventoryMaterialTypes.RoughTube), // 圆棒穿孔实际消耗的是荒管
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
                        join e in _context.WorkOrderExecutionSummaries.AsNoTracking() on p.SourceWorkOrderNo equals e.WorkOrderNo into ej
                        from e in ej.DefaultIfEmpty()
                        select new
                        {
                            p.OrderNo,
                            p.OrderDate,
                            p.RequiredDate,
                            p.LastArrivalDate,
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
                            WoSpecification = w.Specification,
                            ExecutionUrgencyLevel = e != null ? e.UrgencyLevel : null,
                            ExecutionRawMaterialLockRemark = e != null ? e.RawMaterialLockRemark : null,
                            ExecutionTheoreticalCutoffDate = e != null ? (DateTime?)e.TheoreticalCutoffDate : null
                        };

            var all = await query.ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["OrderNo"] = all.Select(x => x.OrderNo).Distinct().OrderBy(x => x).ToList(),
                ["SourceWorkOrderNo"] = all.Where(x => x.SourceWorkOrderNo != null).Select(x => x.SourceWorkOrderNo!).Distinct().OrderBy(x => x).ToList(),
                ["OrderDate"] = all.Select(x => x.OrderDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["RequiredDate"] = all.Select(x => x.RequiredDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["LastArrivalDate"] = all.Where(x => x.LastArrivalDate != null)
                    .Select(x => x.LastArrivalDate!.Value.ToString("yyyy-MM-dd"))
                    .Distinct().OrderBy(x => x).ToList(),
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
                // 空值（无工单号/读模型无记录）以哨兵 "__EXCEL_FILTER_NULL__" 输出，供筛选下拉「空值」选项体现
                ["ExecutionUrgencyLevel"] = all
                    .Select(x => x.ExecutionUrgencyLevel ?? FilterNull)
                    .Distinct()
                    .OrderBy(x => x == FilterNull ? 0 : 1).ThenBy(x => x)
                    .ToList(),
                ["ExecutionRawMaterialLockRemark"] = all
                    .Select(x => x.ExecutionRawMaterialLockRemark ?? FilterNull)
                    .Distinct()
                    .OrderBy(x => x == FilterNull ? 0 : 1).ThenBy(x => x)
                    .ToList(),
                ["ExecutionTheoreticalCutoffDate"] = all
                    .Select(x => x.ExecutionTheoreticalCutoffDate != null ? x.ExecutionTheoreticalCutoffDate.Value.ToString("yyyy-MM-dd") : FilterNull)
                    .Distinct()
                    .OrderBy(x => x == FilterNull ? 0 : 1).ThenBy(x => x)
                    .ToList(),
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
                           join e in _context.WorkOrderExecutionSummaries.AsNoTracking() on p.SourceWorkOrderNo equals e.WorkOrderNo into ej
                           from e in ej.DefaultIfEmpty()
                           where ids.Contains(p.Id)
                           select new { p, w, e, MaterialCategory = p.MaterialCategory }).ToListAsync();

        var dtos = items.Select(x => new PurchaseOrderDto
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
            CreatedBy = x.p.CreatedBy,
            CreatedTime = x.p.CreatedTime,
            UpdatedBy = x.p.UpdatedBy,
            UpdatedTime = x.p.UpdatedTime,
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
            ExecutionScheduleStage = x.e != null ? (int?)x.e.ScheduleStage : null,
            ExecutionUrgencyLevel = x.e != null ? x.e.UrgencyLevel : null,
            ExecutionRawMaterialLockRemark = x.e != null ? x.e.RawMaterialLockRemark : null,
            ExecutionTheoreticalCutoffDate = x.e != null ? (DateTime?)x.e.TheoreticalCutoffDate : null,
        }).ToList();

        // 退货量（内存补充）
        var returnMap = await BuildReturnSummaryAsync(dtos.Select(d => d.OrderNo).ToList());
        foreach (var d in dtos)
        {
            var (rq, rw) = returnMap[d.OrderNo];
            d.ReturnQuantity = rq;
            d.ReturnWeight = rw;
        }
        return dtos;
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
        ["ArrivalDate"] = dto.LastArrivalDate?.ToString("yyyy-MM-dd") ?? "",
        ["Received"] = dto.ReceivedQuantity == 0 && dto.ReceivedWeight == 0m
            ? "-"
            : $"{dto.ReceivedQuantity}支/{dto.ReceivedWeight:G29}kg",
        ["Returned"] = dto.ReturnQuantity == 0 && dto.ReturnWeight == 0m
            ? "-"
            : $"{dto.ReturnQuantity}支/{dto.ReturnWeight:G29}kg",
        ["IsForceCompleted"] = dto.IsForceCompleted ? "是" : "-",
        ["Remark"] = (object?)dto.Remark ?? "",
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
        // 工单实时关注组（与前端 RenderCell 口径一致）
        ["ExecutionScheduleStage"] = dto.ExecutionScheduleStage.HasValue ? IntStatusDisplayHelper.GetScheduleStageText(dto.ExecutionScheduleStage.Value) : "-",
        ["ExecutionUrgencyLevel"] = string.IsNullOrEmpty(dto.ExecutionUrgencyLevel) ? "-" : (DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, dto.ExecutionUrgencyLevel) ?? "-"),
        ["ExecutionRawMaterialLockRemark"] = string.IsNullOrEmpty(dto.ExecutionRawMaterialLockRemark) ? "-" : (DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, dto.ExecutionRawMaterialLockRemark) ?? "-"),
        ["ExecutionTheoreticalCutoffDate"] = dto.ExecutionTheoreticalCutoffDate?.ToString("yyyy-MM-dd") ?? "-",
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
