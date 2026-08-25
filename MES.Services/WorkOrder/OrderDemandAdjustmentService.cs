using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Interfaces.Equipment;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Interfaces.Materials;
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
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;

using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.WorkOrder;

/// <summary>
/// 工单需求调整服务
/// </summary>
public class OrderDemandAdjustmentService : IOrderDemandAdjustmentService
{
    private readonly AppDbContext _context;
    private readonly IWorkOrderExecutionService _workOrderExecutionService;
    private readonly IMemoryCache _cache;

    public OrderDemandAdjustmentService(AppDbContext context, IWorkOrderExecutionService workOrderExecutionService, IMemoryCache cache)
    {
        _context = context;
        _workOrderExecutionService = workOrderExecutionService;
        _cache = cache;
    }

    public async Task<PagedResult<OrderDemandAdjustmentDto>> GetPagedAsync(QueryParams query, DateTime? signDateFrom = null, DateTime? signDateTo = null, DateTime? deliveryDateStart = null, DateTime? deliveryDateEnd = null)
    {
        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var urgingQuery = _context.Set<OrderDemandAdjustment>().AsNoTracking();

        // LEFT JOIN: WorkOrderExecutionSummary LEFT JOIN OrderDemandAdjustment
        var q = from e in summaryQuery
                join u in urgingQuery on e.WorkOrderId equals u.WorkOrderId into uj
                from u in uj.DefaultIfEmpty()
                join wo in _context.WorkOrders.AsNoTracking() on e.WorkOrderId equals wo.Id into woj
                from wo in woj.DefaultIfEmpty()
                select new OrderDemandAdjustmentDto
                {
                    Id = e.Id,
                    WorkOrderId = e.WorkOrderId,
                    WorkOrderNo = e.WorkOrderNo,
                    Salesman = e.Salesman,
                    CustomerName = e.CustomerName,
                    EndCustomer = e.EndCustomer,
                    SignDate = e.SignDate,
                    DeliveryDate = e.DeliveryDate,
                    DelayPenalty = e.DelayPenalty,
                    SettlementMethod = string.IsNullOrEmpty(e.SettlementMethod) ? default : Enum.Parse<SettlementMethod>(e.SettlementMethod),
                    SalesOrderNo = e.SalesOrderNo,
                    ProductionMainNo = e.ProductionMainNo,
                    ProductionSubNo = e.ProductionSubNo,
                    MaterialName = e.MaterialName,
                    DeliveryState = string.IsNullOrEmpty(e.DeliveryState) ? default : Enum.Parse<DeliveryState>(e.DeliveryState),
                    PlantGrade = e.PlantGrade,
                    Specification = e.Specification,
                    LengthStatus = string.IsNullOrEmpty(e.LengthStatus) ? default : Enum.Parse<LengthStatus>(e.LengthStatus),
                    MinLength = e.MinLength,
                    MaxLength = e.MaxLength,
                    TotalItemCount = e.TotalItemCount,
                    TotalQuantity = e.TotalQuantity,
                    TotalMeters = e.TotalMeters,
                    TotalWeight = e.TotalWeight,
                    ScheduleStage = e.ScheduleStage,
                    TotalRemainingWorkDays = e.TotalRemainingWorkDays,
                    CapacityWorkDays = e.CapacityWorkDays,
                    UrgencyLevel = e.UrgencyLevel,
                    EstimatedProcessCompletionDate = e.EstimatedProcessCompletionDate,
                    DaysDiffFromDelivery = e.DaysDiffFromDelivery,
                    RawMaterialLockRemark = e.RawMaterialLockRemark,
                    IsUrging = u != null && u.IsUrging,
                    IsBatchDelivery = u != null && u.IsBatchDelivery,
                    IsPaused = u != null && u.IsPaused,
                    IsForceCompleted = u != null && u.IsForceCompleted,
                    AdjustmentRemark = u != null ? u.AdjustmentRemark : null,
                    CreatedBy = wo != null ? wo.CreatedBy : null,
                    CreatedTime = wo != null ? wo.CreatedTime : default,
                    UpdatedBy = wo != null ? wo.UpdatedBy : null,
                    UpdatedTime = wo != null ? wo.UpdatedTime : default,
                };

        // 订单日期范围筛选（签订日期）
        if (signDateFrom.HasValue)
            q = q.Where(x => x.SignDate >= signDateFrom.Value);
        if (signDateTo.HasValue)
            q = q.Where(x => x.SignDate <= signDateTo.Value);

        // 交货日期范围筛选
        if (deliveryDateStart.HasValue)
            q = q.Where(x => x.DeliveryDate >= deliveryDateStart.Value);
        if (deliveryDateEnd.HasValue)
            q = q.Where(x => x.DeliveryDate <= deliveryDateEnd.Value);

        // 关键词搜索
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            q = q.Where(x =>
                x.WorkOrderNo.Contains(kw) ||
                x.SalesOrderNo.Contains(kw) ||
                x.Salesman.Contains(kw) ||
                x.CustomerName.Contains(kw) ||
                (x.EndCustomer != null && x.EndCustomer.Contains(kw)) ||
                (x.ProductionSubNo != null && x.ProductionSubNo.Contains(kw)) ||
                x.PlantGrade.Contains(kw) ||
                x.Specification.Contains(kw) ||
                x.ProductionMainNo.Contains(kw) ||
                x.MaterialName.Contains(kw) ||
                (x.UrgencyLevel != null && x.UrgencyLevel.Contains(kw)) ||
                (x.RawMaterialLockRemark != null && x.RawMaterialLockRemark.Contains(kw)) ||
                (x.AdjustmentRemark != null && x.AdjustmentRemark.Contains(kw)));
        }

        // 筛选
        q = q.ApplyFilters(query.Filters);

        // 排序
        q = ApplySorting(q, query.SortBy, query.IsDescending);

        var totalCount = await q.CountAsync();
        var items = await q
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<OrderDemandAdjustmentDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<bool> SaveUrgingAsync(int workOrderId, bool isUrging, bool isBatchDelivery, bool isPaused, bool isForceCompleted, string? adjustmentRemark)
    {
        // 联动连带：不允许"工单暂停"，只能"主号暂停"。
        // 联动范围 = 同主号（SalesOrderNo + ProductionMainNo）下未入库完结的工单（WoWarehousingStatus != 2），
        // 已闭环（入库完结/超额）的工单不被暂停/恢复牵连。
        // 强制完成同暂停：主号级联动，置是后同主号未完结工单保持一致（ScheduleStage=主号完成）。
        // 互斥：暂停与强制完成不能同时为真（前端 Switch 互斥，此处后端兜底保留强制完成、解除暂停）。
        if (isPaused && isForceCompleted)
        {
            isPaused = false;
        }

        var currentSummary = await _context.Set<WorkOrderExecutionSummary>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.WorkOrderId == workOrderId);

        var affectedWorkOrderIds = new List<int> { workOrderId };
        if (currentSummary != null && !string.IsNullOrEmpty(currentSummary.SalesOrderNo))
        {
            var siblings = await _context.Set<WorkOrderExecutionSummary>()
                .AsNoTracking()
                .Where(s => s.SalesOrderNo == currentSummary.SalesOrderNo
                    && s.ProductionMainNo == currentSummary.ProductionMainNo
                    && s.WorkOrderId != workOrderId
                    && s.WoWarehousingStatus != 2)
                .Select(s => s.WorkOrderId)
                .ToListAsync();
            affectedWorkOrderIds.AddRange(siblings);
        }

        var existingAdjustments = await _context.Set<OrderDemandAdjustment>()
            .Where(u => affectedWorkOrderIds.Contains(u.WorkOrderId))
            .ToListAsync();
        var existingByWorkOrderId = existingAdjustments.ToDictionary(u => u.WorkOrderId);

        foreach (var wid in affectedWorkOrderIds)
        {
            var isCurrent = wid == workOrderId;
            if (existingByWorkOrderId.TryGetValue(wid, out var adj))
            {
                if (isCurrent)
                {
                    adj.IsUrging = isUrging;
                    adj.IsBatchDelivery = isBatchDelivery;
                    adj.AdjustmentRemark = adjustmentRemark;
                }
                // 暂停/强制完成联动同步：同主号下未完结工单保持一致（两者互斥）
                adj.IsPaused = isPaused;
                adj.IsForceCompleted = isForceCompleted;
                _context.Entry(adj).State = EntityState.Modified;
            }
            else
            {
                _context.Set<OrderDemandAdjustment>().Add(new OrderDemandAdjustment
                {
                    WorkOrderId = wid,
                    IsUrging = isCurrent ? isUrging : false,
                    IsBatchDelivery = isCurrent ? isBatchDelivery : false,
                    IsPaused = isPaused,
                    IsForceCompleted = isForceCompleted,
                    AdjustmentRemark = isCurrent ? adjustmentRemark : null,
                });
            }
        }

        await _context.SaveChangesAsync();

        // 实时同步读模型：IsPaused 变化需立即反映到 WorkOrderExecutionSummary.UrgencyLevel（E停）及关注状态
        // 增量刷新：刷新当前工单及其同主号下被联动改动的工单（从 WorkOrders 取工单号，保证无 summary 行也能刷新）
        var affectedNos = (await _context.WorkOrders
                .AsNoTracking()
                .Where(w => affectedWorkOrderIds.Contains(w.Id))
                .Select(w => w.WorkOrderNo)
                .ToListAsync())
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (affectedNos.Count > 0)
        {
            await _workOrderExecutionService.RefreshByWorkOrderNosAsync(affectedNos);
        }

        return true;
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("OrderDemandAdjustmentService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var query = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();

            var all = await query
                .Select(s => new
                {
                    s.WorkOrderId,
                    s.WorkOrderNo,
                    s.Salesman,
                    s.CustomerName,
                    s.EndCustomer,
                    s.SalesOrderNo,
                    s.ProductionMainNo,
                    s.ProductionSubNo,
                    s.PlantGrade,
                    s.Specification,
                    s.UrgencyLevel,
                    s.RawMaterialLockRemark,
                })
                .ToListAsync();

            // AdjustmentRemark 来自 OrderDemandAdjustment 表（LEFT JOIN）
            var workOrderIds = all.Select(x => x.WorkOrderId).Distinct().ToHashSet();
            var adjustmentRemarks = workOrderIds.Count > 0
                ? await _context.Set<OrderDemandAdjustment>()
                    .Where(u => workOrderIds.Contains(u.WorkOrderId))
                    .Where(u => u.AdjustmentRemark != null)
                    .Select(u => u.AdjustmentRemark!)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync()
                : new List<string>();

            return new Dictionary<string, List<string>>
            {
                ["WorkOrderNo"] = all.Select(x => x.WorkOrderNo).Distinct().OrderBy(x => x).ToList(),
                ["Salesman"] = all.Select(x => x.Salesman).Distinct().OrderBy(x => x).ToList(),
                ["CustomerName"] = all.Select(x => x.CustomerName).Distinct().OrderBy(x => x).ToList(),
                ["EndCustomer"] = all.Where(x => x.EndCustomer != null).Select(x => x.EndCustomer!).Distinct().OrderBy(x => x).ToList(),
                ["SalesOrderNo"] = all.Select(x => x.SalesOrderNo).Distinct().OrderBy(x => x).ToList(),
                ["ProductionMainNo"] = all.Select(x => x.ProductionMainNo).Distinct().OrderBy(x => x).ToList(),
                ["ProductionSubNo"] = all.Where(x => x.ProductionSubNo != null).Select(x => x.ProductionSubNo!).Distinct().OrderBy(x => x).ToList(),
                ["PlantGrade"] = all.Select(x => x.PlantGrade).Distinct().OrderBy(x => x).ToList(),
                ["Specification"] = all.Select(x => x.Specification).Distinct().OrderBy(x => x).ToList(),
                ["UrgencyLevel"] = all.Where(x => x.UrgencyLevel != null).Select(x => x.UrgencyLevel!).Distinct().OrderBy(x => x).ToList(),
                ["RawMaterialLockRemark"] = all.Where(x => x.RawMaterialLockRemark != null).Select(x => x.RawMaterialLockRemark!).Distinct().OrderBy(x => x).ToList(),
                ["AdjustmentRemark"] = adjustmentRemarks,
            };

        }) ?? new Dictionary<string, List<string>>();
    }

    private static IQueryable<OrderDemandAdjustmentDto> ApplySorting(
        IQueryable<OrderDemandAdjustmentDto> query, string? sortBy, bool isDescending)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? query.OrderByDescending(x => x.ScheduleStage)
            : query.ApplySort(sortBy, isDescending);
    }

    public async Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, DateTime? signDateFrom, DateTime? signDateTo, DateTime? deliveryDateStart, DateTime? deliveryDateEnd, List<PrintColumnDef> columns)
    {
        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var urgingQuery = _context.Set<OrderDemandAdjustment>().AsNoTracking();

        var q = from e in summaryQuery
                join u in urgingQuery on e.WorkOrderId equals u.WorkOrderId into uj
                from u in uj.DefaultIfEmpty()
                join wo in _context.WorkOrders.AsNoTracking() on e.WorkOrderId equals wo.Id into woj
                from wo in woj.DefaultIfEmpty()
                select new OrderDemandAdjustmentDto
                {
                    Id = e.Id,
                    WorkOrderId = e.WorkOrderId,
                    WorkOrderNo = e.WorkOrderNo,
                    Salesman = e.Salesman,
                    CustomerName = e.CustomerName,
                    EndCustomer = e.EndCustomer,
                    SignDate = e.SignDate,
                    DeliveryDate = e.DeliveryDate,
                    DelayPenalty = e.DelayPenalty,
                    SettlementMethod = string.IsNullOrEmpty(e.SettlementMethod) ? default : Enum.Parse<SettlementMethod>(e.SettlementMethod),
                    SalesOrderNo = e.SalesOrderNo,
                    ProductionMainNo = e.ProductionMainNo,
                    ProductionSubNo = e.ProductionSubNo,
                    MaterialName = e.MaterialName,
                    DeliveryState = string.IsNullOrEmpty(e.DeliveryState) ? default : Enum.Parse<DeliveryState>(e.DeliveryState),
                    PlantGrade = e.PlantGrade,
                    Specification = e.Specification,
                    LengthStatus = string.IsNullOrEmpty(e.LengthStatus) ? default : Enum.Parse<LengthStatus>(e.LengthStatus),
                    MinLength = e.MinLength,
                    MaxLength = e.MaxLength,
                    TotalItemCount = e.TotalItemCount,
                    TotalQuantity = e.TotalQuantity,
                    TotalMeters = e.TotalMeters,
                    TotalWeight = e.TotalWeight,
                    ScheduleStage = e.ScheduleStage,
                    TotalRemainingWorkDays = e.TotalRemainingWorkDays,
                    CapacityWorkDays = e.CapacityWorkDays,
                    UrgencyLevel = e.UrgencyLevel,
                    EstimatedProcessCompletionDate = e.EstimatedProcessCompletionDate,
                    DaysDiffFromDelivery = e.DaysDiffFromDelivery,
                    RawMaterialLockRemark = e.RawMaterialLockRemark,
                    IsUrging = u != null && u.IsUrging,
                    IsBatchDelivery = u != null && u.IsBatchDelivery,
                    IsPaused = u != null && u.IsPaused,
                    IsForceCompleted = u != null && u.IsForceCompleted,
                    AdjustmentRemark = u != null ? u.AdjustmentRemark : null,
                    CreatedBy = wo != null ? wo.CreatedBy : null,
                    CreatedTime = wo != null ? wo.CreatedTime : default,
                    UpdatedBy = wo != null ? wo.UpdatedBy : null,
                    UpdatedTime = wo != null ? wo.UpdatedTime : default,
                };

        if (signDateFrom.HasValue)
            q = q.Where(x => x.SignDate >= signDateFrom.Value);
        if (signDateTo.HasValue)
            q = q.Where(x => x.SignDate <= signDateTo.Value);

        if (deliveryDateStart.HasValue)
            q = q.Where(x => x.DeliveryDate >= deliveryDateStart.Value);
        if (deliveryDateEnd.HasValue)
            q = q.Where(x => x.DeliveryDate <= deliveryDateEnd.Value);

        if (!string.IsNullOrEmpty(keyword))
        {
            var kw = keyword;
            q = q.Where(x =>
                x.WorkOrderNo.Contains(kw) ||
                x.SalesOrderNo.Contains(kw) ||
                x.Salesman.Contains(kw) ||
                x.CustomerName.Contains(kw) ||
                (x.EndCustomer != null && x.EndCustomer.Contains(kw)) ||
                (x.ProductionSubNo != null && x.ProductionSubNo.Contains(kw)) ||
                x.PlantGrade.Contains(kw) ||
                x.Specification.Contains(kw) ||
                x.ProductionMainNo.Contains(kw) ||
                x.MaterialName.Contains(kw) ||
                (x.UrgencyLevel != null && x.UrgencyLevel.Contains(kw)) ||
                (x.RawMaterialLockRemark != null && x.RawMaterialLockRemark.Contains(kw)) ||
                (x.AdjustmentRemark != null && x.AdjustmentRemark.Contains(kw)));
        }

        q = ApplySorting(q, sortBy, isDescending);

        var items = await q.ToListAsync();

        var resolvedItems = items.Select(item =>
        {
            var dict = new Dictionary<string, object>();
            foreach (var col in columns)
            {
                dict[col.Key] = ResolvePrintValue(item, col.Key);
            }
            return dict;
        }).ToList();

        return OrderDemandAdjustmentPrintHelper.GeneratePdf("工单需求调整", resolvedItems, columns);
    }

    private static object ResolvePrintValue(OrderDemandAdjustmentDto item, string key) => key switch
    {
        "MaterialName" => GetMaterialNameText(item.MaterialName),
        "DeliveryState" => GetDeliveryStateText(item.DeliveryState.ToString()),
        "LengthStatus" => GetWorkOrderLengthStatusText(item.LengthStatus, item.MinLength, item.MaxLength),
        "SettlementMethod" => GetSettlementMethodText(item.SettlementMethod.ToString()),
        "DelayPenalty" => item.DelayPenaltyText,
        "ScheduleStage" => item.ScheduleStageText,
        "IsUrging" => item.IsUrging ? "是" : "否",
        "IsBatchDelivery" => item.IsBatchDelivery ? "是" : "否",
        "IsPaused" => item.IsPaused ? "是" : "否",
        "IsForceCompleted" => item.IsForceCompleted ? "是" : "否",
        "AdjustmentRemark" => item.AdjustmentRemark ?? "",
        "SignDate" => item.SignDate.ToString("yyyy-MM-dd"),
        "DeliveryDate" => item.DeliveryDate.ToString("yyyy-MM-dd"),
        "EstimatedProcessCompletionDate" => item.EstimatedProcessCompletionDate?.ToString("yyyy-MM-dd") ?? "",
        "TotalWeight" => ((int)item.TotalWeight).ToString(),
        "TotalRemainingWorkDays" => item.TotalRemainingWorkDays?.ToString() ?? "",
        "CapacityWorkDays" => item.CapacityWorkDays?.ToString() ?? "",
        "DaysDiffFromDelivery" => item.DaysDiffFromDelivery?.ToString() ?? "",
        _ => GetRawValue(item, key)
    };

    private static string GetRawValue(OrderDemandAdjustmentDto item, string key) => key switch
    {
        "WorkOrderNo" => item.WorkOrderNo ?? "",
        "Salesman" => item.Salesman ?? "",
        "CustomerName" => item.CustomerName ?? "",
        "EndCustomer" => item.EndCustomer ?? "",
        "SalesOrderNo" => item.SalesOrderNo ?? "",
        "ProductionMainNo" => item.ProductionMainNo ?? "",
        "ProductionSubNo" => item.ProductionSubNo ?? "",
        "PlantGrade" => item.PlantGrade ?? "",
        "Specification" => item.Specification ?? "",
        "MinLength" => item.MinLength?.ToString("G29") ?? "",
        "MaxLength" => item.MaxLength?.ToString("G29") ?? "",
        "TotalItemCount" => item.TotalItemCount.ToString(),
        "TotalMeters" => ((int)item.TotalMeters).ToString(),
        "TotalQuantity" => item.TotalQuantity.ToString(),
        "UrgencyLevel" => DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, item.UrgencyLevel) ?? "",
        "RawMaterialLockRemark" => DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, item.RawMaterialLockRemark) ?? "",
        _ => ""
    };

    private static string GetMaterialNameText(string? materialName) => EnumHelper.GetDisplayName<PipeManufacturingType>(materialName);

    private static string GetDeliveryStateText(string? deliveryState) => EnumHelper.GetDisplayName<DeliveryState>(deliveryState);

    private static string GetSettlementMethodText(string? method) => EnumHelper.GetDisplayName<SettlementMethod>(method);

    private static string GetLengthStatusText(string? lengthStatus) => EnumHelper.GetDisplayName<LengthStatus>(lengthStatus);

    /// <summary>工单长度状态中文文本（与前端工单维度 helper 一致：定尺仅"多种"附加标记）</summary>
    private static string GetWorkOrderLengthStatusText(LengthStatus lengthStatus, decimal? minLength, decimal? maxLength)
    {
        if (lengthStatus == LengthStatus.Fixed)
        {
            if (minLength.HasValue && maxLength.HasValue && minLength.Value != maxLength.Value)
                return "定尺（多）";
            return "定尺";
        }
        return GetLengthStatusText(lengthStatus.ToString());
    }

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    public Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        var pdfBytes = OrderDemandAdjustmentPrintHelper.GeneratePdf(title, items, columns);
        return Task.FromResult(pdfBytes);
    }
}
