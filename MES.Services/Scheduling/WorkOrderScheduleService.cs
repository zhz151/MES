using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Scheduling;
using MES.Services.Helpers;

namespace MES.Services.Scheduling;

/// <summary>
/// 工单排程服务（LEFT JOIN 实时查询模式）
/// </summary>
public class WorkOrderScheduleService : IWorkOrderScheduleService
{
    private readonly AppDbContext _context;

    public WorkOrderScheduleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<WorkOrderScheduleDto>> GetPagedAsync(QueryParams query)
    {
        // WorkOrderExecutionSummary LEFT JOIN OrderDemandAdjustment
        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var urgingQuery = _context.Set<OrderDemandAdjustment>().AsNoTracking();

        var q = from e in summaryQuery
                join u in urgingQuery on e.WorkOrderId equals u.WorkOrderId into uj
                from u in uj.DefaultIfEmpty()
                select new WorkOrderScheduleDto
                {
                    Id = e.Id,
                    WorkOrderId = e.WorkOrderId,
                    WorkOrderNo = e.WorkOrderNo,

                    // G1
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

                    // G7
                    FlowOutputRatio = e.FlowOutputRatio,
                    FlowStatus = e.FlowStatus,
                    MainNoFlowOutputRatio = e.MainNoFlowOutputRatio,
                    MainNoFlowStatus = e.MainNoFlowStatus,
                    FlowTotalBatchCount = e.FlowTotalBatchCount,
                    FlowIncompleteBatchCount = e.FlowIncompleteBatchCount,
                    FlowMaxRemainingWorkDays = e.FlowMaxRemainingWorkDays,

                    // G12
                    ScheduleStage = e.ScheduleStage,
                    TotalRemainingWorkDays = e.TotalRemainingWorkDays,
                    CapacityWorkDays = e.CapacityWorkDays,
                    UrgencyLevel = e.UrgencyLevel,
                    EstimatedProcessCompletionDate = e.EstimatedProcessCompletionDate,
                    DaysDiffFromDelivery = e.DaysDiffFromDelivery,
                    RawMaterialLockRemark = e.RawMaterialLockRemark,

                    // G13
                    IsUrging = u != null && u.IsUrging,
                    IsBatchDelivery = u != null && u.IsBatchDelivery,
                    IsPaused = u != null && u.IsPaused,
                    AdjustmentRemark = u != null ? u.AdjustmentRemark : null,

                    // G14
                    PendingSectionRoughTube = e.PendingSectionRoughTube,
                    PendingSectionWarehouseFix = e.PendingSectionWarehouseFix,
                    PendingSection60Roll = e.PendingSection60Roll,
                    PendingSection50Roll = e.PendingSection50Roll,
                    PendingSection30Roll = e.PendingSection30Roll,
                    PendingSection20Roll = e.PendingSection20Roll,
                    PendingSectionThreeRoll = e.PendingSectionThreeRoll,
                    PendingSectionDrawBench = e.PendingSectionDrawBench,
                    DeformedProcessCompleted = e.DeformedProcessCompleted,
                    ProductionAttentionProcess = e.ProductionAttentionProcess == null || e.ProductionAttentionProcess == "-"
                        ? "收尾-成检"
                        : e.ProductionAttentionProcess,
                };

        // 筛选条件：生产执行(ScheduleStage==2) 或 催单+分批交货的原料锁定工单
        q = q.Where(x => x.ScheduleStage == 2
            || (x.ScheduleStage == 1 && x.IsUrging && x.IsBatchDelivery));

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
                (x.AdjustmentRemark != null && x.AdjustmentRemark.Contains(kw)) ||
                (x.ProductionAttentionProcess != null && x.ProductionAttentionProcess.Contains(kw)));
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

        return new PagedResult<WorkOrderScheduleDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var query = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var urgingQuery = _context.Set<OrderDemandAdjustment>().AsNoTracking();

        var joined = from e in query
                     join u in urgingQuery on e.WorkOrderId equals u.WorkOrderId into uj
                     from u in uj.DefaultIfEmpty()
                     select new
                     {
                         e.WorkOrderId,
                         e.WorkOrderNo,
                         e.Salesman,
                         e.CustomerName,
                         e.SalesOrderNo,
                         e.ProductionMainNo,
                         e.ProductionSubNo,
                         e.PlantGrade,
                         e.Specification,
                         e.UrgencyLevel,
                         e.RawMaterialLockRemark,
                         AdjustmentRemark = u != null ? u.AdjustmentRemark : null,
                         e.ProductionAttentionProcess,
                     };

        var all = await joined.ToListAsync();

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
            ["AdjustmentRemark"] = all.Where(x => x.AdjustmentRemark != null).Select(x => x.AdjustmentRemark!).Distinct().OrderBy(x => x).ToList(),
            ["ProductionAttentionProcess"] = all
                .Select(x => x.ProductionAttentionProcess == null || x.ProductionAttentionProcess == "-" ? "收尾-成检" : x.ProductionAttentionProcess)
                .Distinct().OrderBy(x => x).ToList(),
        };
    }

    private static IQueryable<WorkOrderScheduleDto> ApplySorting(
        IQueryable<WorkOrderScheduleDto> query, string? sortBy, bool isDescending)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? query.OrderByDescending(x => x.WorkOrderNo)
            : query.ApplySort(sortBy, isDescending);
    }
}
