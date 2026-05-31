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
/// 原锁计划及执行服务
/// </summary>
public class RawMaterialLockPlanAndExecutionService : IRawMaterialLockPlanAndExecutionService
{
    private readonly AppDbContext _context;
    private readonly IWorkOrderExecutionService _executionService;

    public RawMaterialLockPlanAndExecutionService(AppDbContext context, IWorkOrderExecutionService executionService)
    {
        _context = context;
        _executionService = executionService;
    }

    public async Task<PagedResult<RawMaterialLockPlanAndExecutionDto>> GetPagedAsync(QueryParams query)
    {
        var q = _context.Set<RawMaterialLockPlanAndExecution>().AsNoTracking();

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
                (x.UrgingRemark != null && x.UrgingRemark.Contains(kw)) ||
                (x.CurrentRawMaterialLockRemark != null && x.CurrentRawMaterialLockRemark.Contains(kw)));
        }

        // 筛选
        q = q.ApplyFilters(query.Filters);

        // 排序
        q = ApplySorting(q, query.SortBy, query.IsDescending);

        var totalCount = await q.CountAsync();
        var items = await q
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => new RawMaterialLockPlanAndExecutionDto
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

                // G2
                LatestPlanDate = e.LatestPlanDate,
                MaterialPlanRate = e.MaterialPlanRate,
                MaterialPlanStatus = e.MaterialPlanStatus,
                MainNoMaterialPlanRate = e.MainNoMaterialPlanRate,
                MainNoMaterialPlanStatus = e.MainNoMaterialPlanStatus,
                ProcessCycle = e.ProcessCycle,

                // G5
                PendingRoughTubeQty = e.PendingRoughTubeQty,
                PendingRoughTubeWeight = e.PendingRoughTubeWeight,
                PendingOutsourceFinishQty = e.PendingOutsourceFinishQty,
                PendingOutsourceFinishWeight = e.PendingOutsourceFinishWeight,
                TheoreticalFinishQty = e.TheoreticalFinishQty,
                TheoreticalFinishWeight = e.TheoreticalFinishWeight,

                // G3
                InputStartDate = e.InputStartDate,
                InputEndDate = e.InputEndDate,
                TotalBatchCount = e.TotalBatchCount,
                InputQuantity = e.InputQuantity,
                InputWeight = e.InputWeight,
                TheoreticalOutputQty = e.TheoreticalOutputQty,
                TheoreticalOutputWeight = e.TheoreticalOutputWeight,
                InputOutputRatio = e.InputOutputRatio,
                InputStatus = e.InputStatus,
                MainNoInputOutputRatio = e.MainNoInputOutputRatio,
                MainNoInputStatus = e.MainNoInputStatus,

                // G7
                FlowOutputRatio = e.FlowOutputRatio,
                FlowStatus = e.FlowStatus,
                MainNoFlowOutputRatio = e.MainNoFlowOutputRatio,
                MainNoFlowStatus = e.MainNoFlowStatus,
                FlowTotalBatchCount = e.FlowTotalBatchCount,
                FlowIncompleteBatchCount = e.FlowIncompleteBatchCount,
                FlowMaxRemainingWorkDays = e.FlowMaxRemainingWorkDays,

                // G10
                GeneralDefectWeight = e.GeneralDefectWeight,
                GeneralDefectRatio = e.GeneralDefectRatio,
                SeriousDefectWeight = e.SeriousDefectWeight,
                SeriousDefectRatio = e.SeriousDefectRatio,
                ScrapWeight = e.ScrapWeight,
                ScrapRatio = e.ScrapRatio,

                // G12
                ScheduleStage = e.ScheduleStage,
                TotalRemainingWorkDays = e.TotalRemainingWorkDays,
                UrgencyLevel = e.UrgencyLevel,
                EstimatedProcessCompletionDate = e.EstimatedProcessCompletionDate,
                DaysDiffFromDelivery = e.DaysDiffFromDelivery,
                RawMaterialLockRemark = e.RawMaterialLockRemark,

                // G13
                SalesUrging = e.SalesUrging,
                UrgingRemark = e.UrgingRemark,

                // G14
                CurrentScheduleStage = e.CurrentScheduleStage,
                CurrentRawMaterialLockRemark = e.CurrentRawMaterialLockRemark,
                IsExecuted = e.IsExecuted,
            })
            .ToListAsync();

        return new PagedResult<RawMaterialLockPlanAndExecutionDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<int> PlanArrangementAsync()
    {
        // 1. 先刷新 WorkOrderExecutionSummary
        await _executionService.RefreshAllAsync();

        // 2. 获取 ScheduleStage=1 的工单执行状况 + SalesUrging
        var summaries = await _context.Set<WorkOrderExecutionSummary>()
            .AsNoTracking()
            .Where(e => e.ScheduleStage == 1)
            .ToListAsync();

        var workOrderIds = summaries.Select(s => s.WorkOrderId).ToHashSet();

        var urgings = await _context.Set<SalesUrging>()
            .AsNoTracking()
            .Where(u => workOrderIds.Contains(u.WorkOrderId))
            .ToDictionaryAsync(u => u.WorkOrderId);

        // 3. 删除旧数据
        var existing = await _context.Set<RawMaterialLockPlanAndExecution>().ToListAsync();
        _context.Set<RawMaterialLockPlanAndExecution>().RemoveRange(existing);

        // 4. 插入新数据
        var now = DateTimeOffset.Now;
        var entities = summaries.Select(s =>
        {
            urgings.TryGetValue(s.WorkOrderId, out var urging);
            return new RawMaterialLockPlanAndExecution
            {
                WorkOrderId = s.WorkOrderId,
                WorkOrderNo = s.WorkOrderNo,

                // G1
                Salesman = s.Salesman,
                CustomerName = s.CustomerName,
                SignDate = s.SignDate,
                DeliveryDate = s.DeliveryDate,
                DelayPenalty = s.DelayPenalty,
                SettlementMethod = s.SettlementMethod,
                SalesOrderNo = s.SalesOrderNo,
                ProductionMainNo = s.ProductionMainNo,
                ProductionSubNo = s.ProductionSubNo,
                MaterialName = s.MaterialName,
                DeliveryState = s.DeliveryState,
                PlantGrade = s.PlantGrade,
                Specification = s.Specification,
                LengthStatus = s.LengthStatus,
                MinLength = s.MinLength,
                MaxLength = s.MaxLength,
                TotalItemCount = s.TotalItemCount,
                TotalQuantity = s.TotalQuantity,
                TotalMeters = s.TotalMeters,
                TotalWeight = s.TotalWeight,

                // G2
                LatestPlanDate = s.LatestPlanDate,
                MaterialPlanRate = s.MaterialPlanRate,
                MaterialPlanStatus = s.MaterialPlanStatus,
                MainNoMaterialPlanRate = s.MainNoMaterialPlanRate,
                MainNoMaterialPlanStatus = s.MainNoMaterialPlanStatus,
                ProcessCycle = s.ProcessCycle,

                // G5
                PendingRoughTubeQty = s.PendingRoughTubeQty,
                PendingRoughTubeWeight = s.PendingRoughTubeWeight,
                PendingOutsourceFinishQty = s.PendingOutsourceFinishQty,
                PendingOutsourceFinishWeight = s.PendingOutsourceFinishWeight,
                TheoreticalFinishQty = s.TheoreticalFinishQty,
                TheoreticalFinishWeight = s.TheoreticalFinishWeight,

                // G3
                InputStartDate = s.InputStartDate,
                InputEndDate = s.InputEndDate,
                TotalBatchCount = s.TotalBatchCount,
                InputQuantity = s.InputQuantity,
                InputWeight = s.InputWeight,
                TheoreticalOutputQty = s.TheoreticalOutputQty,
                TheoreticalOutputWeight = s.TheoreticalOutputWeight,
                InputOutputRatio = s.InputOutputRatio,
                InputStatus = s.InputStatus,
                MainNoInputOutputRatio = s.MainNoInputOutputRatio,
                MainNoInputStatus = s.MainNoInputStatus,

                // G7
                FlowOutputRatio = s.FlowOutputRatio,
                FlowStatus = s.FlowStatus,
                MainNoFlowOutputRatio = s.MainNoFlowOutputRatio,
                MainNoFlowStatus = s.MainNoFlowStatus,
                FlowTotalBatchCount = s.FlowTotalBatchCount,
                FlowIncompleteBatchCount = s.FlowIncompleteBatchCount,
                FlowMaxRemainingWorkDays = s.FlowMaxRemainingWorkDays,

                // G10
                GeneralDefectWeight = s.GeneralDefectWeight,
                GeneralDefectRatio = s.GeneralDefectRatio,
                SeriousDefectWeight = s.SeriousDefectWeight,
                SeriousDefectRatio = s.SeriousDefectRatio,
                ScrapWeight = s.ScrapWeight,
                ScrapRatio = s.ScrapRatio,

                // G12
                ScheduleStage = s.ScheduleStage,
                TotalRemainingWorkDays = s.TotalRemainingWorkDays,
                UrgencyLevel = s.UrgencyLevel,
                EstimatedProcessCompletionDate = s.EstimatedProcessCompletionDate,
                DaysDiffFromDelivery = s.DaysDiffFromDelivery,
                RawMaterialLockRemark = s.RawMaterialLockRemark,

                // G13: 从 SalesUrging 取值
                SalesUrging = urging?.IsSalesUrging ?? false,
                UrgingRemark = urging?.UrgingRemark,

                // G14: 快照 = 当前值
                CurrentScheduleStage = s.ScheduleStage,
                CurrentRawMaterialLockRemark = s.RawMaterialLockRemark,
                IsExecuted = null, // 初次安排时未知
            };
        }).ToList();

        _context.Set<RawMaterialLockPlanAndExecution>().AddRange(entities);
        await _context.SaveChangesAsync();

        return entities.Count;
    }

    public async Task<int> ExecuteDataUpdateAsync()
    {
        // 1. 先刷新 WorkOrderExecutionSummary 获取最新数据
        await _executionService.RefreshAllAsync();

        // 2. 获取最新的 ScheduleStage=1 的工单执行状况
        var latestSummaries = await _context.Set<WorkOrderExecutionSummary>()
            .AsNoTracking()
            .Where(e => e.ScheduleStage == 1)
            .ToDictionaryAsync(e => e.WorkOrderId);

        var existingRecords = await _context.Set<RawMaterialLockPlanAndExecution>().ToListAsync();
        var updateCount = 0;

        foreach (var record in existingRecords)
        {
            if (latestSummaries.TryGetValue(record.WorkOrderId, out var latest))
            {
                // 仅更新 G14 快照字段
                record.CurrentScheduleStage = latest.ScheduleStage;
                record.CurrentRawMaterialLockRemark = latest.RawMaterialLockRemark;
                record.IsExecuted = !string.Equals(
                    record.CurrentRawMaterialLockRemark ?? "",
                    record.RawMaterialLockRemark ?? "",
                    StringComparison.Ordinal);

                _context.Entry(record).State = EntityState.Modified;
                updateCount++;
            }
        }

        await _context.SaveChangesAsync();
        return updateCount;
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var query = _context.Set<RawMaterialLockPlanAndExecution>().AsNoTracking();

        var all = await query
            .Select(s => new
            {
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
                s.CurrentRawMaterialLockRemark,
                s.UrgingRemark,
            })
            .ToListAsync();

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
            ["CurrentRawMaterialLockRemark"] = all.Where(x => x.CurrentRawMaterialLockRemark != null).Select(x => x.CurrentRawMaterialLockRemark!).Distinct().OrderBy(x => x).ToList(),
            ["UrgingRemark"] = all.Where(x => x.UrgingRemark != null).Select(x => x.UrgingRemark!).Distinct().OrderBy(x => x).ToList(),
        };
    }

    private static IQueryable<RawMaterialLockPlanAndExecution> ApplySorting(
        IQueryable<RawMaterialLockPlanAndExecution> query, string? sortBy, bool isDescending)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? query.OrderByDescending(x => x.WorkOrderNo)
            : query.ApplySort(sortBy, isDescending);
    }
}
