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
        var planQuery = _context.Set<RawMaterialLockPlanAndExecution>().AsNoTracking();
        var urgingQuery = _context.Set<SalesUrging>().AsNoTracking();

        // LEFT JOIN SalesUrging 获取 G15 字段
        var q = from p in planQuery
                join u in urgingQuery on p.WorkOrderId equals u.WorkOrderId into uj
                from u in uj.DefaultIfEmpty()
                select new RawMaterialLockPlanAndExecutionDto
                {
                    Id = p.Id,
                    WorkOrderId = p.WorkOrderId,
                    WorkOrderNo = p.WorkOrderNo,

                    // G1
                    Salesman = p.Salesman,
                    CustomerName = p.CustomerName,
                    SignDate = p.SignDate,
                    DeliveryDate = p.DeliveryDate,
                    DelayPenalty = p.DelayPenalty,
                    SettlementMethod = p.SettlementMethod,
                    SalesOrderNo = p.SalesOrderNo,
                    ProductionMainNo = p.ProductionMainNo,
                    ProductionSubNo = p.ProductionSubNo,
                    MaterialName = p.MaterialName,
                    DeliveryState = p.DeliveryState,
                    PlantGrade = p.PlantGrade,
                    Specification = p.Specification,
                    LengthStatus = p.LengthStatus,
                    MinLength = p.MinLength,
                    MaxLength = p.MaxLength,
                    TotalItemCount = p.TotalItemCount,
                    TotalQuantity = p.TotalQuantity,
                    TotalMeters = p.TotalMeters,
                    TotalWeight = p.TotalWeight,

                    // G2
                    LatestPlanDate = p.LatestPlanDate,
                    MaterialPlanRate = p.MaterialPlanRate,
                    MaterialPlanStatus = p.MaterialPlanStatus,
                    MainNoMaterialPlanRate = p.MainNoMaterialPlanRate,
                    MainNoMaterialPlanStatus = p.MainNoMaterialPlanStatus,
                    ProcessCycle = p.ProcessCycle,

                    // G5
                    PendingRoughTubeQty = p.PendingRoughTubeQty,
                    PendingRoughTubeWeight = p.PendingRoughTubeWeight,
                    PendingOutsourceFinishQty = p.PendingOutsourceFinishQty,
                    PendingOutsourceFinishWeight = p.PendingOutsourceFinishWeight,
                    TheoreticalFinishQty = p.TheoreticalFinishQty,
                    TheoreticalFinishWeight = p.TheoreticalFinishWeight,

                    // G3
                    InputStartDate = p.InputStartDate,
                    InputEndDate = p.InputEndDate,
                    TotalBatchCount = p.TotalBatchCount,
                    InputQuantity = p.InputQuantity,
                    InputWeight = p.InputWeight,
                    TheoreticalOutputQty = p.TheoreticalOutputQty,
                    TheoreticalOutputWeight = p.TheoreticalOutputWeight,
                    InputOutputRatio = p.InputOutputRatio,
                    InputStatus = p.InputStatus,
                    MainNoInputOutputRatio = p.MainNoInputOutputRatio,
                    MainNoInputStatus = p.MainNoInputStatus,

                    // G7
                    FlowOutputRatio = p.FlowOutputRatio,
                    FlowStatus = p.FlowStatus,
                    MainNoFlowOutputRatio = p.MainNoFlowOutputRatio,
                    MainNoFlowStatus = p.MainNoFlowStatus,
                    FlowTotalBatchCount = p.FlowTotalBatchCount,
                    FlowIncompleteBatchCount = p.FlowIncompleteBatchCount,
                    FlowMaxRemainingWorkDays = p.FlowMaxRemainingWorkDays,

                    // G10
                    GeneralDefectWeight = p.GeneralDefectWeight,
                    GeneralDefectRatio = p.GeneralDefectRatio,
                    SeriousDefectWeight = p.SeriousDefectWeight,
                    SeriousDefectRatio = p.SeriousDefectRatio,
                    ScrapWeight = p.ScrapWeight,
                    ScrapRatio = p.ScrapRatio,

                    // G12
                    ScheduleStage = p.ScheduleStage,
                    TotalRemainingWorkDays = p.TotalRemainingWorkDays,
                    UrgencyLevel = p.UrgencyLevel,
                    EstimatedProcessCompletionDate = p.EstimatedProcessCompletionDate,
                    DaysDiffFromDelivery = p.DaysDiffFromDelivery,
                    RawMaterialLockRemark = p.RawMaterialLockRemark,

                    // G13
                    SalesUrging = p.SalesUrging,
                    UrgingRemark = p.UrgingRemark,

                    // G14
                    CurrentScheduleStage = p.CurrentScheduleStage,
                    CurrentRawMaterialLockRemark = p.CurrentRawMaterialLockRemark,
                    IsExecuted = p.IsExecuted,

                    // G15: 预执行（页面操作标记）
                    IsPreInput = p.IsPreInput,
                    IsMainNoMaterialComplete = p.IsMainNoMaterialComplete,

                    // 从 SalesUrging LEFT JOIN 读取
                    EstimatedArrivalDate = u != null ? u.EstimatedArrivalDate : null,
                    IsLockConfirmed = u != null && u.IsLockConfirmed,

                    // 看板筛选 - 异常标记
                    HasAbnormality = p.DaysDiffFromDelivery != null && p.DaysDiffFromDelivery < 0
                        || (p.ScheduleStage == 1 && u != null && u.IsLockConfirmed && !u.IsMainNoMaterialComplete)
                        || p.TotalRemainingWorkDays != null && p.TotalRemainingWorkDays < 0,
                };

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

                // G15: 预执行（计划安排重置）
                IsPreInput = false,
                IsMainNoMaterialComplete = false,

                // 看板筛选 - 异常标记
                HasAbnormality = s.DaysDiffFromDelivery < 0
                    || (s.ScheduleStage == 1 && (urging?.IsLockConfirmed ?? false) && !(urging?.IsMainNoMaterialComplete ?? false))
                    || s.TotalRemainingWorkDays < 0,
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
        var existingWorkOrderIds = existingRecords.Select(r => r.WorkOrderId).ToHashSet();
        var urgings = await _context.Set<SalesUrging>()
            .AsNoTracking()
            .Where(u => existingWorkOrderIds.Contains(u.WorkOrderId))
            .ToDictionaryAsync(u => u.WorkOrderId);
        var updateCount = 0;

        foreach (var record in existingRecords)
        {
            if (latestSummaries.TryGetValue(record.WorkOrderId, out var latest))
            {
                urgings.TryGetValue(record.WorkOrderId, out var urging);

                // 仅更新 G14 快照字段
                record.CurrentScheduleStage = latest.ScheduleStage;
                record.CurrentRawMaterialLockRemark = latest.RawMaterialLockRemark;
                record.IsExecuted = !string.Equals(
                    record.CurrentRawMaterialLockRemark ?? "",
                    record.RawMaterialLockRemark ?? "",
                    StringComparison.Ordinal);

                record.HasAbnormality = latest.DaysDiffFromDelivery < 0
                    || (record.ScheduleStage == 1 && (urging?.IsLockConfirmed ?? false) && !(urging?.IsMainNoMaterialComplete ?? false))
                    || latest.TotalRemainingWorkDays < 0;

                _context.Entry(record).State = EntityState.Modified;
                updateCount++;
            }
        }

        await _context.SaveChangesAsync();
        return updateCount;
    }

    public async Task<int> SetPreExecuteFlagsAsync(List<int> workOrderIds, bool? isPreInput, bool? isMainNoMaterialComplete)
    {
        var records = await _context.Set<RawMaterialLockPlanAndExecution>()
            .Where(r => workOrderIds.Contains(r.WorkOrderId))
            .ToListAsync();

        foreach (var record in records)
        {
            if (isPreInput.HasValue)
                record.IsPreInput = isPreInput.Value;
            if (isMainNoMaterialComplete.HasValue)
                record.IsMainNoMaterialComplete = isMainNoMaterialComplete.Value;
            _context.Entry(record).State = EntityState.Modified;
        }

        return await _context.SaveChangesAsync();
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

    private static IQueryable<RawMaterialLockPlanAndExecutionDto> ApplySorting(
        IQueryable<RawMaterialLockPlanAndExecutionDto> query, string? sortBy, bool isDescending)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? query.OrderByDescending(x => x.WorkOrderNo)
            : query.ApplySort(sortBy, isDescending);
    }
}
