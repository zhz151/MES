using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.ProductionStandard;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Interfaces.Equipment;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Interfaces.Materials;
using MES.Core.Interfaces.ProductionStandard;
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
using MES.Data.Entities.ProductionStandard;
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

    public async Task<PagedResult<OrderDemandAdjustmentDto>> GetPagedAsync(QueryParams query, DateTime? signDateFrom = null, DateTime? signDateTo = null)
    {
        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var urgingQuery = _context.Set<OrderDemandAdjustment>().AsNoTracking();

        // LEFT JOIN: WorkOrderExecutionSummary LEFT JOIN OrderDemandAdjustment
        var q = from e in summaryQuery
                join u in urgingQuery on e.WorkOrderId equals u.WorkOrderId into uj
                from u in uj.DefaultIfEmpty()
                select new OrderDemandAdjustmentDto
                {
                    Id = e.Id,
                    WorkOrderId = e.WorkOrderId,
                    WorkOrderNo = e.WorkOrderNo,
                    Salesman = e.Salesman,
                    CustomerName = e.CustomerName,
                    SignDate = e.SignDate,
                    DeliveryDate = e.DeliveryDate,
                    DelayPenalty = e.DelayPenalty,
                    SettlementMethod = e.SettlementMethod,
                    SalesOrderNo = e.SalesOrderNo,
                    ProductionMainNo = e.ProductionMainNo,
                    ProductionSubNo = e.ProductionSubNo,
                    MaterialName = e.MaterialName,
                    DeliveryState = e.DeliveryState,
                    PlantGrade = e.PlantGrade,
                    Specification = e.Specification,
                    LengthStatus = e.LengthStatus,
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
                    FlowOutputRatio = e.FlowOutputRatio,
                    FlowStatus = e.FlowStatus,
                    MainNoFlowOutputRatio = e.MainNoFlowOutputRatio,
                    MainNoFlowStatus = e.MainNoFlowStatus,
                    FlowTotalBatchCount = e.FlowTotalBatchCount,
                    FlowIncompleteBatchCount = e.FlowIncompleteBatchCount,
                    FlowMaxRemainingWorkDays = e.FlowMaxRemainingWorkDays,
                    IsUrging = u != null && u.IsUrging,
                    IsBatchDelivery = u != null && u.IsBatchDelivery,
                    IsPaused = u != null && u.IsPaused,
                    AdjustmentRemark = u != null ? u.AdjustmentRemark : null,
                };

        // 订单日期范围筛选
        if (signDateFrom.HasValue)
            q = q.Where(x => x.SignDate >= signDateFrom.Value);
        if (signDateTo.HasValue)
            q = q.Where(x => x.SignDate <= signDateTo.Value);

        // 关键词搜索
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            q = q.Where(x =>
                x.WorkOrderNo.Contains(kw) ||
                x.SalesOrderNo.Contains(kw) ||
                x.Salesman.Contains(kw) ||
                x.CustomerName.Contains(kw) ||
                (x.ProductionSubNo != null && x.ProductionSubNo.Contains(kw)) ||
                x.PlantGrade.Contains(kw) ||
                x.Specification.Contains(kw) ||
                x.ProductionMainNo.Contains(kw) ||
                (x.SettlementMethod != null && x.SettlementMethod.Contains(kw)) ||
                x.MaterialName.Contains(kw) ||
                x.DeliveryState.Contains(kw) ||
                x.LengthStatus.Contains(kw) ||
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

    public async Task<bool> SaveUrgingAsync(int workOrderId, bool isUrging, bool isBatchDelivery, bool isPaused, string? adjustmentRemark)
    {
        var existing = await _context.Set<OrderDemandAdjustment>()
            .FirstOrDefaultAsync(u => u.WorkOrderId == workOrderId);

        if (existing != null)
        {
            existing.IsUrging = isUrging;
            existing.IsBatchDelivery = isBatchDelivery;
            existing.IsPaused = isPaused;
            existing.AdjustmentRemark = adjustmentRemark;
            _context.Entry(existing).State = EntityState.Modified;
        }
        else
        {
            _context.Set<OrderDemandAdjustment>().Add(new OrderDemandAdjustment
            {
                WorkOrderId = workOrderId,
                IsUrging = isUrging,
                IsBatchDelivery = isBatchDelivery,
                IsPaused = isPaused,
                AdjustmentRemark = adjustmentRemark,
            });
        }

        await _context.SaveChangesAsync();

        // 实时同步读模型：IsPaused 变化需立即反映到 WorkOrderExecutionSummary.UrgencyLevel（E停）
        // 增量刷新：仅刷新该工单及其同 SalesOrderNo 的兄弟工单
        var workOrderNo = await _context.WorkOrders
            .AsNoTracking()
            .Where(w => w.Id == workOrderId)
            .Select(w => w.WorkOrderNo)
            .FirstOrDefaultAsync();
        if (!string.IsNullOrEmpty(workOrderNo))
        {
            await _workOrderExecutionService.RefreshByWorkOrderNosAsync(new List<string> { workOrderNo });
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

    public async Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, DateTime? signDateFrom, DateTime? signDateTo, List<PrintColumnDef> columns)
    {
        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var urgingQuery = _context.Set<OrderDemandAdjustment>().AsNoTracking();

        var q = from e in summaryQuery
                join u in urgingQuery on e.WorkOrderId equals u.WorkOrderId into uj
                from u in uj.DefaultIfEmpty()
                select new OrderDemandAdjustmentDto
                {
                    Id = e.Id,
                    WorkOrderId = e.WorkOrderId,
                    WorkOrderNo = e.WorkOrderNo,
                    Salesman = e.Salesman,
                    CustomerName = e.CustomerName,
                    SignDate = e.SignDate,
                    DeliveryDate = e.DeliveryDate,
                    DelayPenalty = e.DelayPenalty,
                    SettlementMethod = e.SettlementMethod,
                    SalesOrderNo = e.SalesOrderNo,
                    ProductionMainNo = e.ProductionMainNo,
                    ProductionSubNo = e.ProductionSubNo,
                    MaterialName = e.MaterialName,
                    DeliveryState = e.DeliveryState,
                    PlantGrade = e.PlantGrade,
                    Specification = e.Specification,
                    LengthStatus = e.LengthStatus,
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
                    FlowOutputRatio = e.FlowOutputRatio,
                    FlowStatus = e.FlowStatus,
                    MainNoFlowOutputRatio = e.MainNoFlowOutputRatio,
                    MainNoFlowStatus = e.MainNoFlowStatus,
                    FlowTotalBatchCount = e.FlowTotalBatchCount,
                    FlowIncompleteBatchCount = e.FlowIncompleteBatchCount,
                    FlowMaxRemainingWorkDays = e.FlowMaxRemainingWorkDays,
                    IsUrging = u != null && u.IsUrging,
                    IsBatchDelivery = u != null && u.IsBatchDelivery,
                    IsPaused = u != null && u.IsPaused,
                    AdjustmentRemark = u != null ? u.AdjustmentRemark : null,
                };

        if (signDateFrom.HasValue)
            q = q.Where(x => x.SignDate >= signDateFrom.Value);
        if (signDateTo.HasValue)
            q = q.Where(x => x.SignDate <= signDateTo.Value);

        if (!string.IsNullOrEmpty(keyword))
        {
            var kw = keyword;
            q = q.Where(x =>
                x.WorkOrderNo.Contains(kw) ||
                x.SalesOrderNo.Contains(kw) ||
                x.Salesman.Contains(kw) ||
                x.CustomerName.Contains(kw) ||
                (x.ProductionSubNo != null && x.ProductionSubNo.Contains(kw)) ||
                x.PlantGrade.Contains(kw) ||
                x.Specification.Contains(kw) ||
                x.ProductionMainNo.Contains(kw) ||
                (x.SettlementMethod != null && x.SettlementMethod.Contains(kw)) ||
                x.MaterialName.Contains(kw) ||
                x.DeliveryState.Contains(kw) ||
                x.LengthStatus.Contains(kw) ||
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

        return TablePrintHelper.GeneratePdf("工单需求调整", resolvedItems, columns);
    }

    private static object ResolvePrintValue(OrderDemandAdjustmentDto item, string key) => key switch
    {
        "MaterialName" => GetMaterialNameText(item.MaterialName),
        "DeliveryState" => GetDeliveryStateText(item.DeliveryState),
        "LengthStatus" => GetLengthStatusText(item.LengthStatus),
        "SettlementMethod" => GetSettlementMethodText(item.SettlementMethod),
        "DelayPenalty" => item.DelayPenaltyText,
        "ScheduleStage" => item.ScheduleStageText,
        "FlowStatus" => item.FlowStatus switch { 0 => "未投料", 1 => "部分", 2 => "满足", _ => "未知" },
        "MainNoFlowStatus" => item.MainNoFlowStatus switch { 0 => "未计划", 1 => "部分", 2 => "满足", _ => "未知" },
        "IsUrging" => item.IsUrging ? "是" : "否",
        "IsBatchDelivery" => item.IsBatchDelivery ? "是" : "否",
        "IsPaused" => item.IsPaused ? "是" : "否",
        "AdjustmentRemark" => item.AdjustmentRemark ?? "",
        "SignDate" => item.SignDate.ToString("yyyy-MM-dd"),
        "DeliveryDate" => item.DeliveryDate.ToString("yyyy-MM-dd"),
        "EstimatedProcessCompletionDate" => item.EstimatedProcessCompletionDate?.ToString("yyyy-MM-dd") ?? "",
        "FlowOutputRatio" => item.FlowOutputRatio.ToString("F1") + "%",
        "MainNoFlowOutputRatio" => item.MainNoFlowOutputRatio.ToString("F1") + "%",
        "TotalWeight" => ((int)item.TotalWeight).ToString(),
        "TotalRemainingWorkDays" => item.TotalRemainingWorkDays?.ToString(),
        "CapacityWorkDays" => item.CapacityWorkDays?.ToString(),
        "DaysDiffFromDelivery" => item.DaysDiffFromDelivery?.ToString(),
        "FlowMaxRemainingWorkDays" => item.FlowMaxRemainingWorkDays.ToString(),
        _ => GetRawValue(item, key)
    };

    private static string GetRawValue(OrderDemandAdjustmentDto item, string key) => key switch
    {
        "WorkOrderNo" => item.WorkOrderNo ?? "",
        "Salesman" => item.Salesman ?? "",
        "CustomerName" => item.CustomerName ?? "",
        "SalesOrderNo" => item.SalesOrderNo ?? "",
        "ProductionMainNo" => item.ProductionMainNo ?? "",
        "ProductionSubNo" => item.ProductionSubNo ?? "",
        "PlantGrade" => item.PlantGrade ?? "",
        "Specification" => item.Specification ?? "",
        "TotalQuantity" => item.TotalQuantity.ToString(),
        "UrgencyLevel" => item.UrgencyLevel ?? "",
        "RawMaterialLockRemark" => item.RawMaterialLockRemark ?? "",
        "FlowTotalBatchCount" => item.FlowTotalBatchCount.ToString(),
        "FlowIncompleteBatchCount" => item.FlowIncompleteBatchCount.ToString(),
        _ => ""
    };

    private static string GetMaterialNameText(string? materialName) => materialName switch
    {
        "SeamlessPipe" => "无缝管",
        "WeldedPipe" => "焊管",
        _ => materialName ?? ""
    };

    private static string GetDeliveryStateText(string? deliveryState) => deliveryState switch
    {
        "SolutionAnnealedAndPickled" => "固溶酸洗",
        "SolutionAnnealedAndPickledUTube" => "固溶酸洗-U型管",
        "SolutionAnnealedAndPickledExternalPolished" => "固溶酸洗-外抛光",
        "SolutionAnnealedAndPickledInternalPolished" => "固溶酸洗-内抛光",
        "SolutionAnnealedAndPickledBothPolished" => "固溶酸洗-内外抛光",
        "SolutionAnnealedAndPickledCoiled" => "固溶酸洗-盘管",
        "Bright" => "光亮",
        "BrightUTube" => "光亮-U型管",
        "BrightCoiled" => "光亮-盘管",
        "Hard" => "硬态",
        _ => deliveryState ?? ""
    };

    private static string GetSettlementMethodText(string? method) => method switch
    {
        "Theoretical" => "理算",
        "Weighing" => "过磅",
        "WeighingNegative" => "过磅-负",
        _ => method ?? ""
    };

    private static string GetLengthStatusText(string? lengthStatus) => lengthStatus switch
    {
        "Fixed" => "定尺",
        "Range" => "范围尺",
        "NonFixed" => "非定尺",
        _ => lengthStatus ?? ""
    };
}
