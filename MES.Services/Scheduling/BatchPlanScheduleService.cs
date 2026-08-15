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
    private readonly IProcessDefinitionService _processDefService;

    public BatchPlanScheduleService(AppDbContext context, IProcessDefinitionService processDefService)
    {
        _context = context;
        _processDefService = processDefService;
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
            // 只持久化手工编辑字段（暂停/抢单/备注）。流转字段（IsFlow/FlowLevel/流转位等）由计划安排 PlanAllAsync 生成维护，
            // 此处一律不写——前端读时覆盖后传入的流转字段是"覆盖后假值"，若写入会破坏 DB 原流转，导致切回"否"无法复原
            existing.IsPaused = dto.IsPaused;
            existing.IsGrabOrder = dto.IsGrabOrder;
            existing.PlanRemark = dto.PlanRemark;
        }
        else
        {
            _context.BatchPlanSchedules.Add(new BatchPlanSchedule
            {
                BatchId = dto.BatchId,
                IsPaused = dto.IsPaused,
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

    /// <summary>调度工段 Tab 归一：中文 Tab 名 → 稳定 Key（工序优先，工段次之）；检验类特殊 Tab 名（过程检验/成品检验/荒管检/在制检）保持中文。</summary>
    private static string? NormalizeSectionTab(string? sectionTab)
    {
        if (string.IsNullOrEmpty(sectionTab)) return sectionTab;
        return ProcessKeys.ToKey(sectionTab) ?? SectionKeys.ToKey(sectionTab) ?? sectionTab;
    }

    /// <summary>
    /// 实时排程档位 → 薄表等级（V5.28 五档映射，与前端 PlanLevelFromScheduleTier 保持一致）：
    /// 急+→1 急+ / 急→2 急 / 急-→3 急- / 顺·带→4 一般 / 略→5 略
    /// </summary>
    private static int MapScheduleTierToPlanLevel(int tier) => tier switch
    {
        1 => 1, // 急+
        2 => 2, // 急
        3 => 3, // 急-
        4 => 4, // 顺 → 一般
        5 => 4, // 带 → 一般
        _ => 5, // 略
    };

    public async Task<bool> PlanAllAsync(string? sectionTab)
    {
        // 中文 Tab 名归一为 Key（工序/工段），检验类特殊 Tab 名保持中文
        sectionTab = NormalizeSectionTab(sectionTab);

        var crKeys = await _processDefService.GetColdRollOrDrawKeysAsync();
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
        var coldRollTabs = new HashSet<string>
        {
            ProcessKeys.ColdRoll60, ProcessKeys.ColdRoll50, ProcessKeys.ColdRoll30, ProcessKeys.ColdRoll20,
            ProcessKeys.ThreeRollColdRoll, ProcessKeys.ColdDraw
        };
        if (!string.IsNullOrEmpty(sectionTab))
        {
            if (coldRollTabs.Contains(sectionTab))
            {
                joined = joined.Where(x =>
                    (x.b.CurrentSectionCompleted == false &&
                     x.b.CurrentGroupName != null && x.b.CurrentGroupName.Contains(sectionTab) &&
                     x.b.CurrentSectionName == SectionKeys.ColdRollDraw) ||
                    (x.b.CurrentSectionCompleted != false &&
                     x.b.NextProcess != null && x.b.NextProcess.Contains(sectionTab) &&
                     x.b.NextSectionName == SectionKeys.ColdRollDraw));
            }
            else if (sectionTab == "过程检验" || sectionTab == "成品检验" || sectionTab == "荒管检" || sectionTab == "在制检")
            {
                if (sectionTab == "成品检验")
                {
                    joined = joined.Where(x =>
                        (x.b.CurrentSectionCompleted == false && x.b.CurrentSectionName == SectionKeys.Inspection &&
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id)
                             .Max(pg => (int?)pg.SequenceNumber) ==
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id && pg.ProcessName == x.b.CurrentGroupName)
                             .Select(pg => (int?)pg.SequenceNumber)
                             .FirstOrDefault()) ||
                        (x.b.CurrentSectionCompleted != false && x.b.NextSectionName == SectionKeys.Inspection && x.b.NextProcess != null &&
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
                        (x.b.CurrentSectionCompleted == false && x.b.CurrentSectionName == SectionKeys.Inspection &&
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id)
                             .Max(pg => (int?)pg.SequenceNumber) >
                         _context.Set<ProcessGroup>()
                             .Where(pg => pg.ProductionBatchId == x.b.Id && pg.ProcessName == x.b.CurrentGroupName)
                             .Select(pg => (int?)pg.SequenceNumber)
                             .FirstOrDefault()) ||
                        (x.b.CurrentSectionCompleted != false && x.b.NextSectionName == SectionKeys.Inspection && x.b.NextProcess != null &&
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
                            (x.b.CurrentSectionCompleted == false && x.b.CurrentGroupName == ProcessKeys.RoughTubeProcessing) ||
                            (x.b.CurrentSectionCompleted != false && x.b.NextProcess == ProcessKeys.RoughTubeProcessing));
                    }
                    else if (sectionTab == "在制检")
                    {
                        joined = joined.Where(x =>
                            (x.b.CurrentSectionCompleted == false && x.b.CurrentGroupName == ProcessKeys.InProcessRepair) ||
                            (x.b.CurrentSectionCompleted != false && x.b.NextProcess == ProcessKeys.InProcessRepair));
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
            // G4（COALESCE：工单计划薄表优先，无覆盖则回退系统值；summary 关注状态 5 档映射到排程 4 档：0/1→0 完成、2→1 原料锁定、3→2 生产执行、4→3 成品检验）
            ScheduleStage = x.plan != null && x.plan.ScheduleStage != null
                ? x.plan.ScheduleStage.Value
                : (x.s != null
                    ? (x.s.ScheduleStage == 0 || x.s.ScheduleStage == 1 ? 0
                        : x.s.ScheduleStage == 2 ? 1
                        : x.s.ScheduleStage == 3 ? 2
                        : x.s.ScheduleStage == 4 ? 3
                        : x.s.ScheduleStage)
                    : 0),
            MainNoAttentionProcess = x.plan != null && x.plan.ProductionAttentionProcess != null ? x.plan.ProductionAttentionProcess : (x.s != null ? x.s.MainNoAttentionProcess : null),
            ProductionFlowProperty = x.plan != null && x.plan.ProductionFlowProperty != null ? x.plan.ProductionFlowProperty : (x.s != null ? x.s.ProductionFlowProperty : null),
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

        // 6. 计算并 Upsert（V5.28 三规则：规则(1) 冷轧排程优先 → 规则(2) 重点生产批次兜底 → 规则(3) 降级）
        foreach (var b in batchData)
        {
            // 构造 G11 计算属性辅助对象（读实时 IsFlow/ScheduleTier/FlowTarget/FlowCRType/OuterDiameterSpan/FlowExecSpec/TargetSequence）
            var dto = new BatchPlanDto
            {
                BatchId = b.Id,
                CurrentSectionCompleted = b.CurrentSectionCompleted,
                CurrentGroupName = b.CurrentGroupName,
                CurrentSectionName = b.CurrentSectionName,
                CurrentSpec = b.CurrentSpec,
                CorrespondingSpec = b.CorrespondingSpec,
                CurrentEquipmentName = b.CurrentEquipmentName,
                CurrentOutsource = b.CurrentOutsource,
                NextProcess = b.NextProcess,
                NextSectionName = b.NextSectionName,
                UrgencyLevel = b.UrgencyLevel,
                ProductionFlowProperty = b.ProductionFlowProperty,
                MainNoAttentionProcess = b.MainNoAttentionProcess,
            };

            // 判定字段（未产/无工序组批次也纳入，执行序由当前工序组推导）
            List<ProcessGroup>? pgs = null;
            if (pgLookup.TryGetValue(b.Id, out var pgList) && pgList.Count > 0)
            {
                pgs = pgList;
                var currentPg = pgs.FirstOrDefault(pg => pg.ProcessName == b.CurrentGroupName);
                dto.ExecutionSequence = currentPg?.GetSectionSequence(b.CurrentSectionName);
                dto.AttentionProcessSectionSequence = BatchPlanService.ComputeAttentionProcessSectionSequence(pgs, b.MainNoAttentionProcess, crKeys);
                BatchPlanService.ComputeColdRollDimensions(dto, pgs, scheduleLookup, crKeys);
            }
            dto.IsKeyBatch = BatchPlanService.ComputeIsKeyBatch(dto, crKeys);

            // 三规则填充薄表字段
            bool planIsFlow;
            int planFlowLevel;
            string? planFlowTarget, planFlowCRType, planOuterDiameterSpan, planFlowExecSpec;
            int? planTargetSequence, planExecutionSequence;

            if (dto.IsFlow)   // 规则(1) 冷轧排程优先：按关联冷轧排程值
            {
                planIsFlow = true;
                planFlowLevel = MapScheduleTierToPlanLevel(dto.ScheduleTier);
                planFlowTarget = dto.FlowTarget;
                planFlowCRType = dto.FlowCRType;
                planOuterDiameterSpan = dto.OuterDiameterSpan;
                planFlowExecSpec = dto.FlowExecSpec;
                planTargetSequence = dto.TargetSequence;
                planExecutionSequence = dto.ExecutionSequence;
            }
            else if (dto.IsKeyBatch)   // 规则(2) 重点生产批次兜底
            {
                planIsFlow = true;
                planFlowLevel = 2; // 急
                // 流转目标按冷轧类型补充档位：荒管处理→荒管检 / 在制修检→在制检 / 生产收尾→成品检验 / 空→null / 剩余→冷轧
                planFlowTarget = MapFlowTargetByCRType(dto.MainNoAttentionProcess);
                planFlowCRType = dto.MainNoAttentionProcess;
                planOuterDiameterSpan = null;
                // 执行规格：生产收尾 → 状态跟踪组执行规格（PendingSpec）；其余 → 主号关注工序对应工序组的规格
                planFlowExecSpec = dto.MainNoAttentionProcess == ProductionAttentionKeys.Finish
                    ? dto.PendingSpec
                    : pgs?.FirstOrDefault(pg => pg.ProcessName == dto.MainNoAttentionProcess)?.ManufacturingSpec;
                planTargetSequence = dto.AttentionProcessSectionSequence;
                planExecutionSequence = dto.ExecutionSequence;
            }
            else   // 规则(3) 降级（执行序仍按状态跟踪"现执行序"填入，供执行反馈组原/现工量差判定；流转=否）
            {
                planIsFlow = false;
                planFlowLevel = 5; // 略
                planFlowTarget = null;
                planFlowCRType = null;
                planOuterDiameterSpan = null;
                planFlowExecSpec = null;
                planTargetSequence = 0;
                planExecutionSequence = dto.ExecutionSequence;
            }

            // 计划备注默认值 = 关联冷轧排程的待轧设备号（可手工再更改；已有非空备注保留不覆盖）
            var planRemark = string.IsNullOrEmpty(dto.CR_SchedMachineNo) ? null : dto.CR_SchedMachineNo;

            // Upsert（保留原有 抢单 和 备注）
            if (existingLookup.TryGetValue(b.Id, out var existing))
            {
                existing.IsFlow = planIsFlow;
                existing.FlowLevel = planFlowLevel;
                existing.FlowTarget = planFlowTarget;
                existing.FlowCRType = planFlowCRType;
                existing.PlanOuterDiameterSpan = planOuterDiameterSpan;
                existing.FlowExecSpec = planFlowExecSpec;
                existing.TargetSequence = planTargetSequence;
                existing.ExecutionSequence = planExecutionSequence;
                // 备注为空时补填默认设备号（手工非空备注不覆盖）
                if (string.IsNullOrEmpty(existing.PlanRemark))
                    existing.PlanRemark = planRemark;
            }
            else
            {
                _context.BatchPlanSchedules.Add(new BatchPlanSchedule
                {
                    BatchId = b.Id,
                    IsFlow = planIsFlow,
                    FlowLevel = planFlowLevel,
                    FlowTarget = planFlowTarget,
                    FlowCRType = planFlowCRType,
                    PlanOuterDiameterSpan = planOuterDiameterSpan,
                    FlowExecSpec = planFlowExecSpec,
                    TargetSequence = planTargetSequence,
                    ExecutionSequence = planExecutionSequence,
                    IsGrabOrder = false,
                    PlanRemark = planRemark,
                });
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 规则2 流转目标按主号关注工序（冷轧类型）补充档位：
    /// 荒管处理→荒管检、在制修检→在制检、生产收尾→成品检验、空→null、剩余（冷轧类工序）→冷轧。
    /// 先归一 ProcessKeys.ToKey（已是英文 Key 幂等、中文反查），生产收尾为特殊值不属工序先判。
    /// </summary>
    private static string? MapFlowTargetByCRType(string? crType)
    {
        if (string.IsNullOrEmpty(crType)) return null;   // '' → null
        if (crType == ProductionAttentionKeys.Finish) return FlowTargetKeys.FinalCheck;   // 生产收尾 → 成品检验
        var key = ProcessKeys.ToKey(crType) ?? crType;
        if (key == ProcessKeys.RoughTubeProcessing) return FlowTargetKeys.RoughTubeCheck; // 荒管处理 → 荒管检
        if (key == ProcessKeys.InProcessRepair) return FlowTargetKeys.InProcessCheck;     // 在制修检 → 在制检
        return FlowTargetKeys.ColdRoll;                                                   // 剩余（冷轧类工序）→ 冷轧
    }

    private static BatchPlanScheduleDto ToDto(BatchPlanSchedule entity)
    {
        return new BatchPlanScheduleDto
        {
            Id = entity.Id,
            BatchId = entity.BatchId,
            IsPaused = entity.IsPaused,
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

}
