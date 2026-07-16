using Microsoft.EntityFrameworkCore;
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
using MES.Core.Enums;
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
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Scheduling;
using MES.Services.Helpers;

namespace MES.Services.Scheduling;

/// <summary>
/// 原锁计划服务（LEFT JOIN 实时查询）
/// </summary>
public class RawMaterialLockPlanAndExecutionService : IRawMaterialLockPlanAndExecutionService
{
    private readonly AppDbContext _context;

    public RawMaterialLockPlanAndExecutionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<RawMaterialLockPlanAndExecutionDto>> GetPagedAsync(QueryParams query)
    {
        // G1-G12+G13: WorkOrderExecutionSummary（仅 ScheduleStage=1）
        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking()
            .Where(e => e.ScheduleStage == 1);
        // G15: RawMaterialLockPreExecution
        var preExecQuery = _context.Set<RawMaterialLockPreExecution>().AsNoTracking();

        // LEFT JOIN RawMaterialLockPreExecution（G13 直接从实体读取，无需 JOIN）
        var q = from e in summaryQuery
                join p in preExecQuery on e.WorkOrderId equals p.WorkOrderId into pj
                from p in pj.DefaultIfEmpty()
                select new RawMaterialLockPlanAndExecutionDto
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

                    // G2
                    LatestPlanDate = e.LatestPlanDate,
                    MaterialPlanRate = e.MaterialPlanRate,
                    MaterialPlanStatus = (MaterialPlanStatus)e.MaterialPlanStatus,
                    MainNoMaterialPlanRate = e.MainNoMaterialPlanRate,
                    MainNoMaterialPlanStatus = (MaterialPlanStatus)e.MainNoMaterialPlanStatus,
                    ProcessCycle = e.ProcessCycle,
                    MaterialPlanCoveredCount = e.MaterialPlanCoveredCount,
                    MaterialPlanProportion = e.MaterialPlanProportion,
                    LatestRequiredDate = e.LatestRequiredDate,

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
                    CapacityWorkDays = e.CapacityWorkDays,
                    UrgencyLevel = e.UrgencyLevel,
                    EstimatedProcessCompletionDate = e.EstimatedProcessCompletionDate,
                    DaysDiffFromDelivery = e.DaysDiffFromDelivery,
                    RawMaterialLockRemark = e.RawMaterialLockRemark,

                    // G13: 直接从实体读取（已由 RefreshAllAsync 同步）
                    IsUrging = e.IsUrging,
                    IsBatchDelivery = e.IsBatchDelivery,
                    IsPaused = e.IsPaused,
                    AdjustmentRemark = e.AdjustmentRemark,

                    // G15: 实时 LEFT JOIN RawMaterialLockPreExecution
                    IsPreInput = p != null && p.IsPreInput,
                    BudgetInputDate = p != null ? p.BudgetInputDate : null,
                    IsBudgetComplete = p != null && p.IsBudgetComplete,
                    IsMainNoMaterialComplete = p != null && p.IsMainNoMaterialComplete,

                    // 看板筛选 - 异常标记
                    HasAbnormality = e.DaysDiffFromDelivery != null && e.DaysDiffFromDelivery < 0
                        || e.TotalRemainingWorkDays != null && e.TotalRemainingWorkDays < 0,
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
                (x.SettlementMethod.ToString().Contains(kw)) ||
                x.MaterialName.Contains(kw) ||
                (x.DeliveryState.ToString().Contains(kw)) ||
                (x.LengthStatus.ToString().Contains(kw)) ||
                (x.UrgencyLevel != null && x.UrgencyLevel.Contains(kw)) ||
                (x.RawMaterialLockRemark != null && x.RawMaterialLockRemark.Contains(kw)) ||
                (x.AdjustmentRemark != null && x.AdjustmentRemark.Contains(kw)));
        }

        // 筛选
        q = q.ApplyFilters(query.Filters);

        // 排序
        q = ApplySorting(q, query.SortBy, query.IsDescending);

        var totalCount = await q.CountAsync();

        // 汇总: 待检验到料批次（IsPreInput=true）
        var preInputCount = await q.CountAsync(x => x.IsPreInput);
        var preInputWeight = await q.Where(x => x.IsPreInput).SumAsync(x => x.TotalWeight);

        var items = await q
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<RawMaterialLockPlanAndExecutionDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize,
            Extras = new Dictionary<string, object>
            {
                ["preInputCount"] = preInputCount,
                ["preInputWeight"] = preInputWeight
            }
        };
    }

    public async Task<SetPreExecuteFlagsResult> SetPreExecuteFlagsAsync(List<int> workOrderIds, bool? isPreInput, bool? isMainNoMaterialComplete, DateTime? budgetInputDate = null, bool? isBudgetComplete = null)
    {
        // Upsert G15 记录（一个工单一条）
        var existingRecords = await _context.Set<RawMaterialLockPreExecution>()
            .Where(r => workOrderIds.Contains(r.WorkOrderId))
            .ToListAsync();

        foreach (var workOrderId in workOrderIds)
        {
            var record = existingRecords.FirstOrDefault(r => r.WorkOrderId == workOrderId);
            if (record == null)
            {
                record = new RawMaterialLockPreExecution { WorkOrderId = workOrderId };
                _context.Set<RawMaterialLockPreExecution>().Add(record);
            }

            if (isPreInput.HasValue)
                record.IsPreInput = isPreInput.Value;
            if (isMainNoMaterialComplete.HasValue)
                record.IsMainNoMaterialComplete = isMainNoMaterialComplete.Value;
            if (budgetInputDate.HasValue)
                record.BudgetInputDate = budgetInputDate.Value;
            else if (isPreInput == false)
                record.BudgetInputDate = null;
            if (isBudgetComplete.HasValue)
                record.IsBudgetComplete = isBudgetComplete.Value;
        }

        var count = await _context.SaveChangesAsync();

        if (isPreInput.HasValue)
        {
            await RecalculateMainNoCompleteAsync(workOrderIds);
            await _context.SaveChangesAsync();
        }
        else if (isBudgetComplete.HasValue)
        {
            if (isBudgetComplete.Value)
            {
                // IsBudgetComplete=true：保存标记 + 系统自动计算 + 覆盖强制 true
                await RecalculateMainNoCompleteAsync(workOrderIds);
                await _context.SaveChangesAsync();
            }
            else
            {
                // IsBudgetComplete=false：手工判定，直接强制整组 IsMainNoMaterialComplete=false
                await ForceGroupMainNoIncompleteAsync(workOrderIds);
                await _context.SaveChangesAsync();
            }
        }

        var parts = new List<string>();
        if (isPreInput.HasValue)
            parts.Add(isPreInput.Value ? "执行" : "取消执行");
        if (budgetInputDate.HasValue)
            parts.Add("预算投料日");
        if (isMainNoMaterialComplete.HasValue)
            parts.Add(isMainNoMaterialComplete.Value ? "主号齐全" : "取消主号");
        if (isBudgetComplete.HasValue)
            parts.Add(isBudgetComplete.Value ? "预算主号齐全" : "取消预算主号齐全");
        var msg = $"标记完成（{string.Join(",", parts)}），共{count}条";

        return new SetPreExecuteFlagsResult { Count = count, Message = msg };
    }

    /// <summary>
    /// 主号齐全系统计算（支持设 true 和回退 false）
    /// </summary>
    private async Task RecalculateMainNoCompleteAsync(List<int> workOrderIds)
    {
        var summaries = await _context.Set<WorkOrderExecutionSummary>()
            .Where(s => workOrderIds.Contains(s.WorkOrderId))
            .ToListAsync();

        // 收集受影响的同组工单ID（避免重复处理）
        var allAffectedIds = new HashSet<int>();

        foreach (var summary in summaries)
        {
            switch (summary.RawMaterialLockRemark)
            {
                case "A质量影响":
                    allAffectedIds.Add(summary.WorkOrderId);
                    break;

                case "B已购未回":
                case "C计划未执行":
                case "D未完善计划":
                    var sameGroupIds = await _context.Set<WorkOrderExecutionSummary>()
                        .Where(s => s.SalesOrderNo == summary.SalesOrderNo
                                 && s.ProductionMainNo == summary.ProductionMainNo
                                 && s.RawMaterialLockRemark == summary.RawMaterialLockRemark
                                 && s.ScheduleStage == 1)
                        .Select(s => s.WorkOrderId)
                        .ToListAsync();
                    foreach (var id in sameGroupIds)
                        allAffectedIds.Add(id);
                    break;

                default:
                    // null/无备注工单（ScheduleStage!=1 或 MainNoMaterialPlanStatus=2 等）
                    // 按 (订单号, 主号) 扩展同组工单（IsBudgetComplete 覆盖按此分组）
                    var otherGroupIds = await _context.Set<WorkOrderExecutionSummary>()
                        .Where(s => s.SalesOrderNo == summary.SalesOrderNo
                                 && s.ProductionMainNo == summary.ProductionMainNo
                                 && s.ScheduleStage == 1)
                        .Select(s => s.WorkOrderId)
                        .ToListAsync();
                    foreach (var id in otherGroupIds)
                        allAffectedIds.Add(id);
                    break;
            }
        }

        if (allAffectedIds.Count == 0) return;

        // 批量加载所有受影响的 PreExecution 记录
        var allPreExecs = await _context.Set<RawMaterialLockPreExecution>()
            .Where(r => allAffectedIds.Contains(r.WorkOrderId))
            .ToListAsync();

        var allSummaries = await _context.Set<WorkOrderExecutionSummary>()
            .Where(s => allAffectedIds.Contains(s.WorkOrderId))
            .ToListAsync();

        var summaryDict = allSummaries.ToDictionary(s => s.WorkOrderId);

        // ---- A质量影响：直接跟随 IsPreInput ----
        foreach (var pre in allPreExecs)
        {
            if (!summaryDict.TryGetValue(pre.WorkOrderId, out var s)) continue;
            if (s.RawMaterialLockRemark != "A质量影响") continue;

            if (pre.IsMainNoMaterialComplete != pre.IsPreInput)
                pre.IsMainNoMaterialComplete = pre.IsPreInput;
        }

        // ---- B/C/D：按 (订单号, 主号, 原锁备注) 分组处理 ----
        var groupCases = new[] { "B已购未回", "C计划未执行", "D未完善计划" };
        var processedGroups = new HashSet<(string SalesOrderNo, string MainNo, string Remark)>();

        foreach (var pre in allPreExecs)
        {
            if (!summaryDict.TryGetValue(pre.WorkOrderId, out var s)) continue;
            if (!groupCases.Contains(s.RawMaterialLockRemark)) continue;

            var groupKey = (s.SalesOrderNo, s.ProductionMainNo, s.RawMaterialLockRemark!);
            if (!processedGroups.Add(groupKey)) continue; // 已处理

            // 获取组内所有工单ID
            var groupIds = allSummaries
                .Where(x => x.SalesOrderNo == s.SalesOrderNo
                         && x.ProductionMainNo == s.ProductionMainNo
                         && x.RawMaterialLockRemark == s.RawMaterialLockRemark
                         && x.ScheduleStage == 1)
                .Select(x => x.WorkOrderId)
                .ToHashSet();

            // 组内全部就绪 → 全部主号齐全
            // 某工单"就绪"条件：用户标记执行 OR (B类型+G5物料已回) OR (C/D类型+G7已流转)
            var shouldBeComplete = groupIds.All(id =>
            {
                // 条件1：用户显式标记执行
                if (allPreExecs.Any(r => r.WorkOrderId == id && r.IsPreInput))
                    return true;

                if (!summaryDict.TryGetValue(id, out var sum)) return false;

                // 条件2：B已购未回 — G5待回荒管=0 且 待回外购成=0 → 物料已回
                if (s.RawMaterialLockRemark == "B已购未回")
                    return sum.PendingRoughTubeQty == 0 && sum.PendingOutsourceFinishQty == 0;

                // 条件3：C计划未执行 / D未完善计划 — G7流转状态≥1 → 已开始流转
                return sum.FlowStatus >= 1;
            });

            // 更新组内所有现有 PreExecution 记录
            foreach (var memberPre in allPreExecs.Where(r => groupIds.Contains(r.WorkOrderId)))
            {
                if (memberPre.IsMainNoMaterialComplete != shouldBeComplete)
                    memberPre.IsMainNoMaterialComplete = shouldBeComplete;
            }

            // 组内无 PreExecution 的成员创建一条
            var existingIds = allPreExecs
                .Where(r => groupIds.Contains(r.WorkOrderId))
                .Select(r => r.WorkOrderId)
                .ToHashSet();
            foreach (var missingId in groupIds.Except(existingIds))
            {
                _context.Set<RawMaterialLockPreExecution>().Add(
                    new RawMaterialLockPreExecution
                    {
                        WorkOrderId = missingId,
                        IsPreInput = false,
                        IsMainNoMaterialComplete = shouldBeComplete
                    });
            }
        }

        // ---- 无备注(null)：强制 false ----
        foreach (var pre in allPreExecs)
        {
            if (!summaryDict.TryGetValue(pre.WorkOrderId, out var s)) continue;
            if (s.RawMaterialLockRemark == "A质量影响") continue;
            if (groupCases.Contains(s.RawMaterialLockRemark)) continue;

            // 非 A/B/C/D（ScheduleStage!=1 或无需锁料）：默认 false
            pre.IsMainNoMaterialComplete = false;
        }

        // ---- IsBudgetComplete 覆盖：组内任一工单预算主号齐全 → 全部主号齐全 ----
        var budgetCompleteIds = allPreExecs
            .Where(r => r.IsBudgetComplete)
            .Select(r => r.WorkOrderId)
            .ToHashSet();
        if (budgetCompleteIds.Count > 0)
        {
            var processedOverrideGroups = new HashSet<(string SalesOrderNo, string MainNo, string Remark)>();
            foreach (var pre in allPreExecs)
            {
                if (!summaryDict.TryGetValue(pre.WorkOrderId, out var s)) continue;

                // A质量影响：单工单维度
                if (s.RawMaterialLockRemark == "A质量影响")
                {
                    if (budgetCompleteIds.Contains(pre.WorkOrderId) && !pre.IsMainNoMaterialComplete)
                        pre.IsMainNoMaterialComplete = true;
                    continue;
                }

                // B/C/D：按 (订单号, 主号, 原锁备注) 组维度
                if (!groupCases.Contains(s.RawMaterialLockRemark)) continue;
                var groupKey = (s.SalesOrderNo, s.ProductionMainNo, s.RawMaterialLockRemark!);
                if (!processedOverrideGroups.Add(groupKey)) continue;

                var groupIds = allSummaries
                    .Where(x => x.SalesOrderNo == s.SalesOrderNo
                             && x.ProductionMainNo == s.ProductionMainNo
                             && x.RawMaterialLockRemark == s.RawMaterialLockRemark
                             && x.ScheduleStage == 1)
                    .Select(x => x.WorkOrderId)
                    .ToHashSet();

                // 组内任一工单 IsBudgetComplete=true → 整组 IsMainNoMaterialComplete=true
                if (groupIds.Any(id => budgetCompleteIds.Contains(id)))
                {
                    foreach (var memberPre in allPreExecs.Where(r => groupIds.Contains(r.WorkOrderId)))
                    {
                        if (!memberPre.IsMainNoMaterialComplete)
                            memberPre.IsMainNoMaterialComplete = true;
                    }

                    // 补漏：为组内缺失 PreExecution 记录的成员创建记录
                    var existingIds = allPreExecs
                        .Where(r => groupIds.Contains(r.WorkOrderId))
                        .Select(r => r.WorkOrderId)
                        .ToHashSet();
                    foreach (var missingId in groupIds.Except(existingIds))
                    {
                        _context.Set<RawMaterialLockPreExecution>().Add(
                            new RawMaterialLockPreExecution
                            {
                                WorkOrderId = missingId,
                                IsMainNoMaterialComplete = true
                            });
                    }
                }
            }

            // ---- IsBudgetComplete 覆盖：无备注工单按 (订单号, 主号) 分组 ----
            var handledRemarks = new HashSet<string> { "A质量影响", "B已购未回", "C计划未执行", "D未完善计划" };
            var otherBudgetIds = budgetCompleteIds
                .Where(id => summaryDict.TryGetValue(id, out var s) && !handledRemarks.Contains(s.RawMaterialLockRemark ?? ""))
                .ToHashSet();
            if (otherBudgetIds.Count > 0)
            {
                var processedOtherGroups = new HashSet<(string SalesOrderNo, string MainNo)>();
                foreach (var sum in allSummaries.Where(s => otherBudgetIds.Contains(s.WorkOrderId)))
                {
                    var groupKey = (sum.SalesOrderNo, sum.ProductionMainNo);
                    if (!processedOtherGroups.Add(groupKey)) continue;

                    var groupIds = allSummaries
                        .Where(x => x.SalesOrderNo == sum.SalesOrderNo
                                 && x.ProductionMainNo == sum.ProductionMainNo
                                 && x.ScheduleStage == 1)
                        .Select(x => x.WorkOrderId)
                        .ToHashSet();

                    foreach (var memberPre in allPreExecs.Where(r => groupIds.Contains(r.WorkOrderId)))
                    {
                        if (!memberPre.IsMainNoMaterialComplete)
                            memberPre.IsMainNoMaterialComplete = true;
                    }

                    var existingIds = allPreExecs
                        .Where(r => groupIds.Contains(r.WorkOrderId))
                        .Select(r => r.WorkOrderId)
                        .ToHashSet();
                    foreach (var missingId in groupIds.Except(existingIds))
                    {
                        _context.Set<RawMaterialLockPreExecution>().Add(
                            new RawMaterialLockPreExecution
                            {
                                WorkOrderId = missingId,
                                IsMainNoMaterialComplete = true
                            });
                    }
                }
            }
        }
    }

    /// <summary>
    /// IsBudgetComplete=false：手工判定，直接强制同组全部工单 IsMainNoMaterialComplete=false
    /// </summary>
    private async Task ForceGroupMainNoIncompleteAsync(List<int> workOrderIds)
    {
        var summaries = await _context.Set<WorkOrderExecutionSummary>()
            .Where(s => workOrderIds.Contains(s.WorkOrderId))
            .ToListAsync();
        if (summaries.Count == 0) return;

        var allTargetIds = new HashSet<int>();
        var groupCases = new[] { "B已购未回", "C计划未执行", "D未完善计划" };

        foreach (var summary in summaries)
        {
            switch (summary.RawMaterialLockRemark)
            {
                case "A质量影响":
                    allTargetIds.Add(summary.WorkOrderId);
                    break;

                case "B已购未回":
                case "C计划未执行":
                case "D未完善计划":
                    var groupIds = await _context.Set<WorkOrderExecutionSummary>()
                        .Where(s => s.SalesOrderNo == summary.SalesOrderNo
                                 && s.ProductionMainNo == summary.ProductionMainNo
                                 && s.RawMaterialLockRemark == summary.RawMaterialLockRemark
                                 && s.ScheduleStage == 1)
                        .Select(s => s.WorkOrderId)
                        .ToListAsync();
                    foreach (var id in groupIds)
                        allTargetIds.Add(id);
                    break;

                default:
                    // null/无备注：按 (订单号, 主号) 扩展
                    var otherIds = await _context.Set<WorkOrderExecutionSummary>()
                        .Where(s => s.SalesOrderNo == summary.SalesOrderNo
                                 && s.ProductionMainNo == summary.ProductionMainNo
                                 && s.ScheduleStage == 1)
                        .Select(s => s.WorkOrderId)
                        .ToListAsync();
                    foreach (var id in otherIds)
                        allTargetIds.Add(id);
                    break;
            }
        }

        // 加载现有 PreExecution 记录并设为 false
        var existingPreExecs = await _context.Set<RawMaterialLockPreExecution>()
            .Where(r => allTargetIds.Contains(r.WorkOrderId))
            .ToListAsync();

        foreach (var pre in existingPreExecs)
            pre.IsMainNoMaterialComplete = false;

        // 为缺失 PreExecution 记录的成员创建一条
        var existingIds = existingPreExecs.Select(r => r.WorkOrderId).ToHashSet();
        foreach (var missingId in allTargetIds.Except(existingIds))
        {
            _context.Set<RawMaterialLockPreExecution>().Add(
                new RawMaterialLockPreExecution
                {
                    WorkOrderId = missingId,
                    IsPreInput = false,
                    IsMainNoMaterialComplete = false
                });
        }
    }

    private static IQueryable<RawMaterialLockPlanAndExecutionDto> ApplySorting(
        IQueryable<RawMaterialLockPlanAndExecutionDto> query, string? sortBy, bool isDescending)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? query.OrderByDescending(x => x.WorkOrderNo)
            : query.ApplySort(sortBy, isDescending);
    }
}
