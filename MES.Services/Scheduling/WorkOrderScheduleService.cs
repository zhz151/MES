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
/// 工单排程服务
/// </summary>
public class WorkOrderScheduleService : IWorkOrderScheduleService
{
    private readonly AppDbContext _context;
    private readonly IWorkOrderExecutionService _executionService;

    public WorkOrderScheduleService(AppDbContext context, IWorkOrderExecutionService executionService)
    {
        _context = context;
        _executionService = executionService;
    }

    public async Task<PagedResult<WorkOrderScheduleDto>> GetPagedAsync(QueryParams query)
    {
        var q = _context.Set<WorkOrderSchedule>().AsNoTracking()
            .Select(p => new WorkOrderScheduleDto
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

                // G7
                FlowOutputRatio = p.FlowOutputRatio,
                FlowStatus = p.FlowStatus,
                MainNoFlowOutputRatio = p.MainNoFlowOutputRatio,
                MainNoFlowStatus = p.MainNoFlowStatus,
                FlowTotalBatchCount = p.FlowTotalBatchCount,
                FlowIncompleteBatchCount = p.FlowIncompleteBatchCount,
                FlowMaxRemainingWorkDays = p.FlowMaxRemainingWorkDays,

                // G12
                ScheduleStage = p.ScheduleStage,
                TotalRemainingWorkDays = p.TotalRemainingWorkDays,
                CapacityWorkDays = p.CapacityWorkDays,
                UrgencyLevel = p.UrgencyLevel,
                EstimatedProcessCompletionDate = p.EstimatedProcessCompletionDate,
                DaysDiffFromDelivery = p.DaysDiffFromDelivery,
                RawMaterialLockRemark = p.RawMaterialLockRemark,

                // G13
                SalesUrging = p.SalesUrging,
                UrgingRemark = p.UrgingRemark,
            });

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
                (x.UrgingRemark != null && x.UrgingRemark.Contains(kw)));
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

    public async Task<int> PlanArrangementAsync()
    {
        // 1. 先刷新 WorkOrderExecutionSummary
        await _executionService.RefreshAllAsync();

        // 2. 获取全部工单执行状况 + SalesUrging
        var summaries = await _context.Set<WorkOrderExecutionSummary>()
            .AsNoTracking()
            .ToListAsync();

        var workOrderIds = summaries.Select(s => s.WorkOrderId).ToHashSet();

        var urgings = await _context.Set<SalesUrging>()
            .AsNoTracking()
            .Where(u => workOrderIds.Contains(u.WorkOrderId))
            .ToDictionaryAsync(u => u.WorkOrderId);

        // 3. 筛选符合条件的工单
        //    第一块：关注状态为"生产执行"(ScheduleStage==2)的工单
        var stage2Ids = summaries
            .Where(s => s.ScheduleStage == 2)
            .Select(s => s.WorkOrderId)
            .ToHashSet();

        //    第二块：原锁计划及执行中主号齐全(IsMainNoMaterialComplete==true)的记录，
        //          取相同(ProductionMainNo, SalesOrderNo)在工单执行状况中的所有工单
        var completeMainNos = await _context.Set<RawMaterialLockPlanAndExecution>()
            .AsNoTracking()
            .Where(r => r.IsMainNoMaterialComplete)
            .Select(r => new { r.ProductionMainNo, r.SalesOrderNo })
            .Distinct()
            .ToListAsync();

        var mainNoKeySet = completeMainNos
            .Select(x => (x.ProductionMainNo, x.SalesOrderNo))
            .ToHashSet();

        var relatedIds = summaries
            .Where(s => mainNoKeySet.Contains((s.ProductionMainNo, s.SalesOrderNo)))
            .Select(s => s.WorkOrderId)
            .ToHashSet();

        var allTargetIds = stage2Ids.Union(relatedIds);
        var filtered = summaries.Where(s => allTargetIds.Contains(s.WorkOrderId)).ToList();

        // 4. 删除旧数据
        var existing = await _context.Set<WorkOrderSchedule>().ToListAsync();
        _context.Set<WorkOrderSchedule>().RemoveRange(existing);

        // 5. 插入新数据
        var entities = filtered.Select(s =>
        {
            urgings.TryGetValue(s.WorkOrderId, out var urging);
            return new WorkOrderSchedule
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

                // G7
                FlowOutputRatio = s.FlowOutputRatio,
                FlowStatus = s.FlowStatus,
                MainNoFlowOutputRatio = s.MainNoFlowOutputRatio,
                MainNoFlowStatus = s.MainNoFlowStatus,
                FlowTotalBatchCount = s.FlowTotalBatchCount,
                FlowIncompleteBatchCount = s.FlowIncompleteBatchCount,
                FlowMaxRemainingWorkDays = s.FlowMaxRemainingWorkDays,

                // G12
                ScheduleStage = s.ScheduleStage,
                TotalRemainingWorkDays = s.TotalRemainingWorkDays,
                CapacityWorkDays = s.CapacityWorkDays,
                UrgencyLevel = s.UrgencyLevel,
                EstimatedProcessCompletionDate = s.EstimatedProcessCompletionDate,
                DaysDiffFromDelivery = s.DaysDiffFromDelivery,
                RawMaterialLockRemark = s.RawMaterialLockRemark,

                // G13
                SalesUrging = urging?.IsSalesUrging ?? false,
                UrgingRemark = urging?.UrgingRemark,
            };
        }).ToList();

        _context.Set<WorkOrderSchedule>().AddRange(entities);
        await _context.SaveChangesAsync();

        return entities.Count;
    }

    public async Task<int> ExecuteDataUpdateAsync()
    {
        // 1. 先刷新 WorkOrderExecutionSummary 获取最新数据
        await _executionService.RefreshAllAsync();

        // 2. 获取最新的工单执行状况
        var latestSummaries = await _context.Set<WorkOrderExecutionSummary>()
            .AsNoTracking()
            .ToDictionaryAsync(e => e.WorkOrderId);

        var existingRecords = await _context.Set<WorkOrderSchedule>().ToListAsync();
        var updateCount = 0;

        foreach (var record in existingRecords)
        {
            if (latestSummaries.TryGetValue(record.WorkOrderId, out var latest))
            {
                // 更新 G7 有效流转字段
                record.FlowOutputRatio = latest.FlowOutputRatio;
                record.FlowStatus = latest.FlowStatus;
                record.MainNoFlowOutputRatio = latest.MainNoFlowOutputRatio;
                record.MainNoFlowStatus = latest.MainNoFlowStatus;
                record.FlowTotalBatchCount = latest.FlowTotalBatchCount;
                record.FlowIncompleteBatchCount = latest.FlowIncompleteBatchCount;
                record.FlowMaxRemainingWorkDays = latest.FlowMaxRemainingWorkDays;

                // 更新 G12 实时关注字段
                record.ScheduleStage = latest.ScheduleStage;
                record.TotalRemainingWorkDays = latest.TotalRemainingWorkDays;
                record.CapacityWorkDays = latest.CapacityWorkDays;
                record.UrgencyLevel = latest.UrgencyLevel;
                record.EstimatedProcessCompletionDate = latest.EstimatedProcessCompletionDate;
                record.DaysDiffFromDelivery = latest.DaysDiffFromDelivery;
                record.RawMaterialLockRemark = latest.RawMaterialLockRemark;

                _context.Entry(record).State = EntityState.Modified;
                updateCount++;
            }
        }

        await _context.SaveChangesAsync();
        return updateCount;
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var query = _context.Set<WorkOrderSchedule>().AsNoTracking();

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
            ["UrgingRemark"] = all.Where(x => x.UrgingRemark != null).Select(x => x.UrgingRemark!).Distinct().OrderBy(x => x).ToList(),
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
