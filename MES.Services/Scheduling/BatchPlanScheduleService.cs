using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
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
using MES.Services.Extensions;
using MES.Services.Helpers;

namespace MES.Services.Scheduling;

/// <summary>
/// 批次计划薄表服务 — 单条保存 + 批量计划安排
/// </summary>
public class BatchPlanScheduleService : IBatchPlanScheduleService
{
    private readonly AppDbContext _context;

    public BatchPlanScheduleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<BatchPlanScheduleDto>> GetAllAsync()
    {
        return await _context.BatchPlanSchedules
            .AsNoTracking()
            .OrderBy(b => b.Id)
            .Select(b => ToDto(b))
            .ToListAsync();
    }

    public async Task<bool> SaveAsync(BatchPlanScheduleDto dto)
    {
        var existing = await _context.BatchPlanSchedules
            .FirstOrDefaultAsync(b => b.BatchId == dto.BatchId);

        if (existing != null)
        {
            // 保存全部 10 个计划字段（手动编辑覆盖自动计算值）
            existing.IsFlow = dto.IsFlow;
            existing.FlowLevel = dto.FlowLevel;
            existing.FlowTarget = dto.FlowTarget;
            existing.FlowCRType = dto.FlowCRType;
            existing.PlanOuterDiameterSpan = dto.PlanOuterDiameterSpan;
            existing.FlowExecSpec = dto.FlowExecSpec;
            existing.TargetSequence = dto.TargetSequence;
            existing.ExecutionSequence = dto.ExecutionSequence;
            existing.IsGrabOrder = dto.IsGrabOrder;
            existing.PlanRemark = dto.PlanRemark;
        }
        else
        {
            _context.BatchPlanSchedules.Add(new BatchPlanSchedule
            {
                BatchId = dto.BatchId,
                IsFlow = dto.IsFlow,
                FlowLevel = dto.FlowLevel,
                FlowTarget = dto.FlowTarget,
                FlowCRType = dto.FlowCRType,
                PlanOuterDiameterSpan = dto.PlanOuterDiameterSpan,
                FlowExecSpec = dto.FlowExecSpec,
                TargetSequence = dto.TargetSequence,
                ExecutionSequence = dto.ExecutionSequence,
                IsGrabOrder = dto.IsGrabOrder,
                PlanRemark = dto.PlanRemark,
            });
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> PlanAllAsync(string? sectionTab)
    {
        // 1. 查询活跃批次
        var batchQuery = _context.ProductionBatches.AsNoTracking()
            .Where(b => b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress);

        var summaryQuery = _context.Set<WorkOrderExecutionSummary>().AsNoTracking();
        var planQuery = _context.Set<WorkOrderPlan>().AsNoTracking();

        var joined = from b in batchQuery
                     join s in summaryQuery on b.WorkOrderNo equals s.WorkOrderNo into sj
                     from s in sj.DefaultIfEmpty()
                     join plan in planQuery on s.WorkOrderId equals plan.WorkOrderId into planj
                     from plan in planj.DefaultIfEmpty()
                     select new { b, s, plan };

        // 2. 工段筛选（同 GetAllAsync）
        var coldRollTabs = new HashSet<string> { "60冷轧", "50冷轧", "30冷轧", "20冷轧", "三辊冷轧", "冷拔" };
        if (!string.IsNullOrEmpty(sectionTab))
        {
            if (coldRollTabs.Contains(sectionTab))
            {
                joined = joined.Where(x =>
                    (x.b.CurrentSectionCompleted == false &&
                     x.b.CurrentGroupName != null && x.b.CurrentGroupName.Contains(sectionTab) &&
                     x.b.CurrentSectionName == "冷轧拔") ||
                    (x.b.CurrentSectionCompleted != false &&
                     x.b.NextProcess != null && x.b.NextProcess.Contains(sectionTab) &&
                     x.b.NextSectionName == "冷轧拔"));
            }
            else if (sectionTab == "过程检验" || sectionTab == "成品检验" || sectionTab == "荒管检" || sectionTab == "在制检")
            {
                if (sectionTab == "成品检验")
                {
                    joined = joined.Where(x =>
                        (x.b.CurrentSectionCompleted == false && x.b.CurrentSectionName == "检验" &&
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id)
                             .Max(pg => (int?)pg.SequenceNumber) ==
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id && pg.ProcessName == x.b.CurrentGroupName)
                             .Select(pg => (int?)pg.SequenceNumber)
                             .FirstOrDefault()) ||
                        (x.b.CurrentSectionCompleted != false && x.b.NextSectionName == "检验" && x.b.NextProcess != null &&
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id)
                             .Max(pg => (int?)pg.SequenceNumber) ==
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id && pg.ProcessName == x.b.NextProcess)
                             .Select(pg => (int?)pg.SequenceNumber)
                             .FirstOrDefault()));
                }
                else
                {
                    // 过程检验/荒管检/在制检：工段=检验，且非最大工序值
                    joined = joined.Where(x =>
                        (x.b.CurrentSectionCompleted == false && x.b.CurrentSectionName == "检验" &&
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id)
                             .Max(pg => (int?)pg.SequenceNumber) >
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id && pg.ProcessName == x.b.CurrentGroupName)
                             .Select(pg => (int?)pg.SequenceNumber)
                             .FirstOrDefault()) ||
                        (x.b.CurrentSectionCompleted != false && x.b.NextSectionName == "检验" && x.b.NextProcess != null &&
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id)
                             .Max(pg => (int?)pg.SequenceNumber) >
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id && pg.ProcessName == x.b.NextProcess)
                             .Select(pg => (int?)pg.SequenceNumber)
                             .FirstOrDefault()));

                    // 荒管检/在制检：额外按工序名过滤
                    if (sectionTab == "荒管检")
                    {
                        joined = joined.Where(x =>
                            (x.b.CurrentSectionCompleted == false && x.b.CurrentGroupName == ProcessNames.RoughTubeProcessing) ||
                            (x.b.CurrentSectionCompleted != false && x.b.NextProcess == ProcessNames.RoughTubeProcessing));
                    }
                    else if (sectionTab == "在制检")
                    {
                        joined = joined.Where(x =>
                            (x.b.CurrentSectionCompleted == false && x.b.CurrentGroupName == ProcessNames.InProcessRepair) ||
                            (x.b.CurrentSectionCompleted != false && x.b.NextProcess == ProcessNames.InProcessRepair));
                    }
                }
            }
            else
            {
                joined = joined.Where(x =>
                    (x.b.CurrentSectionCompleted == false &&
                     x.b.CurrentSectionName != null && x.b.CurrentSectionName.Contains(sectionTab)) ||
                    (x.b.CurrentSectionCompleted != false &&
                     x.b.NextSectionName != null && x.b.NextSectionName.Contains(sectionTab)));
            }
        }

        var batchData = await joined.Select(x => new
        {
            x.b.Id,
            x.b.CurrentSectionCompleted,
            x.b.CurrentGroupName,
            x.b.NextProcess,
            x.b.CurrentSectionName,
            x.b.NextSectionName,
            x.b.CurrentSpec,
            x.b.CorrespondingSpec,
            x.b.CurrentEquipmentName,
            x.b.CurrentOutsource,
            UrgencyLevel = x.plan != null && x.plan.UrgencyLevel != null ? x.plan.UrgencyLevel : (x.s != null ? x.s.UrgencyLevel : null),
            ScheduleStage = x.plan != null && x.plan.ScheduleStage != null ? x.plan.ScheduleStage.Value : (x.s != null ? x.s.ScheduleStage : 0),
            MainNoAttentionProcess = x.plan != null && x.plan.ProductionAttentionProcess != null ? x.plan.ProductionAttentionProcess : (x.s != null ? x.s.MainNoAttentionProcess : null),
            IsUrging = x.s != null && x.s.IsUrging,
            IsBatchDelivery = x.s != null && x.s.IsBatchDelivery,
            x.b.PlantGrade,
            x.b.Specification,
            x.b.MinLength,
            x.b.MaxLength,
            x.b.CurrentValidWeight,
        }).ToListAsync();

        if (batchData.Count == 0) return false;

        // 3. 加载 ProcessGroups
        var batchIds = batchData.Select(x => x.Id).ToList();
        var allPgs = await _context.Set<ProcessGroup>()
            .AsNoTracking()
            .Where(pg => batchIds.Contains(pg.ProductionBatchId))
            .OrderBy(pg => pg.ProductionBatchId)
            .ThenBy(pg => pg.SequenceNumber)
            .ToListAsync();
        var pgLookup = allPgs.GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 4. 加载冷轧排程小表
        var scheduleAll = await _context.ColdRollSpecSchedules
            .AsNoTracking()
            .ToListAsync();
        var scheduleLookup = scheduleAll.ToDictionary(
            s => $"{s.ProcessType}|{s.BilletSpec}|{s.RollingSpec}|{s.IsFinished}",
            StringComparer.OrdinalIgnoreCase);

        // 5. 加载已有计划记录
        var existingPlans = await _context.BatchPlanSchedules
            .Where(bp => batchIds.Contains(bp.BatchId))
            .ToListAsync();
        var existingLookup = existingPlans.ToDictionary(bp => bp.BatchId);

        // 6. 计算并 Upsert
        foreach (var b in batchData)
        {
            if (!pgLookup.TryGetValue(b.Id, out var pgs) || pgs.Count == 0)
                continue;

            var pendingProcess = b.CurrentSectionCompleted == false
                ? b.CurrentGroupName
                : b.NextProcess;
            var pendingSectionName = b.CurrentSectionCompleted == false
                ? b.CurrentSectionName
                : b.NextSectionName;
            var pendingPg = pgs.FirstOrDefault(pg => pg.ProcessName == pendingProcess);
            if (pendingPg == null) continue;

            var pendingIdx = pgs.IndexOf(pendingPg);
            var maxSeq = pgs.Max(pg => pg.SequenceNumber);
            var pendingEquipment = b.CurrentSectionCompleted == false
                ? b.CurrentEquipmentName ?? b.CurrentOutsource
                : null;

            // 冷轧维度推导 (简化版，仅用于 IsFlow 判定)
            string? currentCR_ProcessType = null, currentCR_BilletSpec = null, currentCR_RollingSpec = null;
            bool currentCR_IsFinished = false;
            string? nextCR_ProcessType = null, nextCR_BilletSpec = null, nextCR_RollingSpec = null;
            bool nextCR_IsFinished = false;
            string? nextNextCR_ProcessType = null, nextNextCR_BilletSpec = null, nextNextCR_RollingSpec = null;
            bool nextNextCR_IsFinished = false;

            // 匹配结果变量（先声明，下方赋值）
            string? crCompletionType = null, crRollType = null, crSchedMachineNo = null;

            // 冷轧排程（本层）：仅当执行工段=冷轧拔时才填充（与 GetAllAsync 保持一致）
            if (!string.IsNullOrEmpty(pendingProcess) && ProcessNames.IsColdRollOrDraw(pendingProcess)
                && !string.IsNullOrEmpty(pendingSectionName) && pendingSectionName == SectionDefs.ColdRollDraw)
            {
                currentCR_ProcessType = pendingProcess;
                currentCR_RollingSpec = pendingPg.ManufacturingSpec;
                if (pendingIdx > 0)
                    currentCR_BilletSpec = pgs[pendingIdx - 1].ManufacturingSpec;
                currentCR_IsFinished = pendingPg.SequenceNumber == maxSeq;

                // 在轧要求匹配（工序组匹配冷轧小表）
                if (!string.IsNullOrEmpty(pendingEquipment))
                {
                    var curKey = $"{currentCR_ProcessType}|{currentCR_BilletSpec}|{currentCR_RollingSpec}|{currentCR_IsFinished}";
                    if (scheduleLookup.TryGetValue(curKey, out var curSched))
                        crCompletionType = curSched.CompletionType;
                }
            }

            if (pendingIdx + 1 < pgs.Count)
            {
                var nextPg = pgs[pendingIdx + 1];
                if (ProcessNames.IsColdRollOrDraw(nextPg.ProcessName))
                {
                    nextCR_ProcessType = nextPg.ProcessName;
                    nextCR_RollingSpec = nextPg.ManufacturingSpec;
                    nextCR_BilletSpec = pendingPg.ManufacturingSpec;
                    nextCR_IsFinished = nextPg.SequenceNumber == maxSeq;
                }
            }

            if (pendingIdx + 2 < pgs.Count)
            {
                var nextNextPg = pgs[pendingIdx + 2];
                if (ProcessNames.IsColdRollOrDraw(nextNextPg.ProcessName))
                {
                    nextNextCR_ProcessType = nextNextPg.ProcessName;
                    nextNextCR_RollingSpec = nextNextPg.ManufacturingSpec;
                    nextNextCR_BilletSpec = pgs[pendingIdx + 1].ManufacturingSpec;
                    nextNextCR_IsFinished = nextNextPg.SequenceNumber == maxSeq;
                }
            }

            // 待轧要求匹配（三层 else-if 链：currentCR / nextCR / nextNextCR）
            if (!string.IsNullOrEmpty(currentCR_ProcessType) && string.IsNullOrEmpty(pendingEquipment))
            {
                var curKey = $"{currentCR_ProcessType}|{currentCR_BilletSpec}|{currentCR_RollingSpec}|{currentCR_IsFinished}";
                if (scheduleLookup.TryGetValue(curKey, out var curSched))
                { crRollType = curSched.RollType; crSchedMachineNo = curSched.MachineNo; }
            }
            else if (!string.IsNullOrEmpty(nextCR_ProcessType) && string.IsNullOrEmpty(pendingEquipment))
            {
                var nextKey = $"{nextCR_ProcessType}|{nextCR_BilletSpec}|{nextCR_RollingSpec}|{nextCR_IsFinished}";
                if (scheduleLookup.TryGetValue(nextKey, out var nextSched))
                { crRollType = nextSched.RollType; crSchedMachineNo = nextSched.MachineNo; }
            }
            else if (!string.IsNullOrEmpty(nextNextCR_ProcessType) && string.IsNullOrEmpty(pendingEquipment))
            {
                var nextNextKey = $"{nextNextCR_ProcessType}|{nextNextCR_BilletSpec}|{nextNextCR_RollingSpec}|{nextNextCR_IsFinished}";
                if (scheduleLookup.TryGetValue(nextNextKey, out var nextNextSched))
                { crRollType = nextNextSched.RollType; crSchedMachineNo = nextNextSched.MachineNo; }
            }

            // 计算 IsFlow
            var isFlow = false;
            var flowLevel = 5;
            string? flowTarget = null;
            string? flowCRType = null;
            string? flowExecSpec = null;
            var isUrgent = b.UrgencyLevel == "A+急" || b.UrgencyLevel == "A急";
            var isKeyBatch = (b.ScheduleStage == 2 && isUrgent &&
                              (pendingProcess == "荒管处理" ||
                               (b.MainNoAttentionProcess != null && pendingProcess == b.MainNoAttentionProcess
                                   && (!ProcessNames.IsColdRollOrDraw(pendingProcess) || pendingSectionName == SectionDefs.ColdRollDraw)) ||
                               pendingProcess == "收尾-成检")) ||
                             (b.ScheduleStage == 1 && (b.IsUrging || b.IsBatchDelivery) && isUrgent &&
                              (pendingProcess == "荒管处理" ||
                               (b.MainNoAttentionProcess != null && pendingProcess == b.MainNoAttentionProcess
                                   && (!ProcessNames.IsColdRollOrDraw(pendingProcess) || pendingSectionName == SectionDefs.ColdRollDraw)) ||
                               pendingProcess == "收尾-成检"));

            if (b.MainNoAttentionProcess == "收尾-成检")
            {
                isFlow = true;
                flowTarget = "成检";
                flowCRType = "-";
                flowExecSpec = b.CurrentSectionCompleted == false ? b.CurrentSpec : b.CorrespondingSpec;
            }
            else if (!string.IsNullOrEmpty(crCompletionType) && crCompletionType != "None")
            {
                var isPartial1 = isUrgent && (b.ScheduleStage == 2 || (b.ScheduleStage == 1 && (b.IsUrging || b.IsBatchDelivery)));
                var isPartial3 = isUrgent || b.UrgencyLevel == "B顺";
                if (crCompletionType == "All" ||
                    (crCompletionType == "Urgent" && (isKeyBatch || isPartial1)) ||
                    (crCompletionType == "Partial2" && isUrgent) ||
                    (crCompletionType == "Partial3" && isPartial3))
                {
                    isFlow = true;
                    flowTarget = "完工冷轧";
                    flowCRType = pendingProcess;
                    flowExecSpec = currentCR_RollingSpec;
                }
            }

            if (!isFlow && !string.IsNullOrEmpty(crRollType) && crRollType != "None")
            {
                var isPartial1 = isUrgent && (b.ScheduleStage == 2 || (b.ScheduleStage == 1 && (b.IsUrging || b.IsBatchDelivery)));
                var isPartial3 = isUrgent || b.UrgencyLevel == "B顺";
                if (crRollType == "All" ||
                    (crRollType == "Urgent" && (isKeyBatch || isPartial1)) ||
                    (crRollType == "Partial2" && isUrgent) ||
                    (crRollType == "Partial3" && isPartial3))
                {
                    isFlow = true;
                    flowTarget = "冷轧";
                    // Determine roll type process type
                    if (!string.IsNullOrEmpty(currentCR_ProcessType) && string.IsNullOrEmpty(pendingEquipment))
                    { flowCRType = currentCR_ProcessType; flowExecSpec = currentCR_RollingSpec; }
                    else if (!string.IsNullOrEmpty(nextCR_ProcessType) && string.IsNullOrEmpty(pendingEquipment))
                    { flowCRType = nextCR_ProcessType; flowExecSpec = nextCR_RollingSpec; }
                    else if (!string.IsNullOrEmpty(nextNextCR_ProcessType) && string.IsNullOrEmpty(pendingEquipment))
                    { flowCRType = nextNextCR_ProcessType; flowExecSpec = nextNextCR_RollingSpec; }
                }
            }

            if (isFlow)
            {
                if (isKeyBatch)
                    flowLevel = 1;
                else if (isUrgent)
                    flowLevel = 2;
                else if (b.UrgencyLevel == "B顺")
                    flowLevel = 3;
                else
                    flowLevel = 4;
            }
            else
                flowLevel = 5;
            var currentPg = pgs.FirstOrDefault(pg => pg.ProcessName == b.CurrentGroupName);
            var execSeq = currentPg?.GetSectionSequence(b.CurrentSectionName);
            var targetSeq = BatchPlanService.ComputeTargetSequence(pgs, flowTarget, flowCRType);

            // 外径跨度计算（与 G7 同一逻辑）
            string? outerDiameterSpan = null;
            if (b.MainNoAttentionProcess == "收尾-成检")
            {
                outerDiameterSpan = null;
            }
            else if (!string.IsNullOrEmpty(crCompletionType) && crCompletionType != "None")
            {
                outerDiameterSpan = GetShortDisplay(currentCR_BilletSpec, currentCR_RollingSpec);
            }
            else if (!string.IsNullOrEmpty(crRollType) && crRollType != "None")
            {
                var billetSpec = currentCR_BilletSpec;
                var rollingSpec = currentCR_RollingSpec;
                if (string.IsNullOrEmpty(pendingEquipment))
                {
                    if (!string.IsNullOrEmpty(nextCR_ProcessType))
                    { billetSpec = nextCR_BilletSpec; rollingSpec = nextCR_RollingSpec; }
                    else if (!string.IsNullOrEmpty(nextNextCR_ProcessType))
                    { billetSpec = nextNextCR_BilletSpec; rollingSpec = nextNextCR_RollingSpec; }
                }
                outerDiameterSpan = GetShortDisplay(billetSpec, rollingSpec);
            }

            // Upsert
            if (existingLookup.TryGetValue(b.Id, out var existing))
            {
                existing.IsFlow = isFlow;
                existing.FlowLevel = flowLevel;
                existing.FlowTarget = flowTarget;
                existing.FlowCRType = flowCRType;
                existing.PlanOuterDiameterSpan = outerDiameterSpan;
                existing.FlowExecSpec = flowExecSpec;
                existing.TargetSequence = targetSeq;
                existing.ExecutionSequence = execSeq;
                // 保留原有 抢单 和 备注
            }
            else
            {
                _context.BatchPlanSchedules.Add(new BatchPlanSchedule
                {
                    BatchId = b.Id,
                    IsFlow = isFlow,
                    FlowLevel = flowLevel,
                    FlowTarget = flowTarget,
                    FlowCRType = flowCRType,
                    PlanOuterDiameterSpan = outerDiameterSpan,
                    FlowExecSpec = flowExecSpec,
                    TargetSequence = targetSeq,
                    ExecutionSequence = execSeq,
                    IsGrabOrder = false,
                    PlanRemark = null,
                });
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    private static BatchPlanScheduleDto ToDto(BatchPlanSchedule entity)
    {
        return new BatchPlanScheduleDto
        {
            Id = entity.Id,
            BatchId = entity.BatchId,
            IsFlow = entity.IsFlow,
            FlowLevel = entity.FlowLevel,
            FlowTarget = entity.FlowTarget,
            FlowCRType = entity.FlowCRType,
            PlanOuterDiameterSpan = entity.PlanOuterDiameterSpan,
            FlowExecSpec = entity.FlowExecSpec,
            TargetSequence = entity.TargetSequence,
            ExecutionSequence = entity.ExecutionSequence,
            IsGrabOrder = entity.IsGrabOrder,
            PlanRemark = entity.PlanRemark,
        };
    }

    /// <summary>
    /// 外径跨度计算：坯料规格外径-轧制规格外径，如"110-89"
    /// </summary>
    private static string? GetShortDisplay(string? billetSpec, string? rollingSpec)
    {
        var outer1 = billetSpec?.Split('*', '×').FirstOrDefault()?.Trim() ?? "";
        var outer2 = rollingSpec?.Split('*', '×').FirstOrDefault()?.Trim() ?? "";
        return string.IsNullOrEmpty(outer1) || string.IsNullOrEmpty(outer2) ? null : $"{outer1}-{outer2}";
    }
}
