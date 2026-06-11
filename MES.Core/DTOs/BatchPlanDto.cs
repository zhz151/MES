using System.Text.Json.Serialization;

namespace MES.Core.DTOs;

/// <summary>
/// 在产明细计划 DTO — ProductionBatch LEFT JOIN WorkOrderExecutionSummary + WorkOrderPlan
/// </summary>
public class BatchPlanDto
{
    // ===== 内部字段（用于后端推导，不序列化到前端） =====
    [JsonIgnore]
    public int BatchId { get; set; }

    // ===== G1：批次信息 =====
    public string BatchNo { get; set; } = string.Empty;
    public string? TagNo { get; set; }
    public string PlantGrade { get; set; } = string.Empty;
    public decimal? CurrentValidWeight { get; set; }

    // ===== G2：关联工单信息 =====
    public string WorkOrderNo { get; set; } = string.Empty;
    public string? Salesman { get; set; }
    public DateTime DeliveryDate { get; set; }
    public string? DeliveryState { get; set; }
    public string Specification { get; set; } = string.Empty;
    public string? LengthStatus { get; set; }
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }

    // ===== G3：状态跟踪（原始字段，用于计算） =====
    public DateTime? CurrentExecDate { get; set; }
    public bool? CurrentSectionCompleted { get; set; }
    public string? CurrentGroupName { get; set; }
    public string? CurrentSectionName { get; set; }
    public string? CurrentSpec { get; set; }
    public string? CurrentEquipmentName { get; set; }
    public string? CurrentOutsource { get; set; }
    public string? NextSectionName { get; set; }
    public string? NextProcess { get; set; }
    public string? CorrespondingSpec { get; set; }

    /// <summary>执行序（实时：待产执行工序对应 ProcessGroup 的组内序号）</summary>
    public int? ExecutionSequence { get; set; }

    // ===== G4：批次关注（COALESCE：工单计划薄表优先，无覆盖则回退系统值） =====
    public string? UrgencyLevel { get; set; }
    public int ScheduleStage { get; set; }
    public string? MainNoAttentionProcess { get; set; }
    public string? ProductionFlowProperty { get; set; }

    /// <summary>最大剩余工量（天）：此工单号下所有批次中 RemainingWorkDays 的最大值</summary>
    public int? MaxBatchRemainingWorkDays { get; set; }

    // ===== 计算字段（仅前端展示用，不参与 SQL 排序） =====

    /// <summary>待在产执行工序：工段未完工→CurrentGroupName，已完工→NextProcess</summary>
    [JsonIgnore]
    public string? PendingProcess =>
        CurrentSectionCompleted == false ? CurrentGroupName : NextProcess;

    /// <summary>执行工段：工段未完工→CurrentSectionName，已完工→NextSectionName</summary>
    [JsonIgnore]
    public string? PendingSectionName =>
        CurrentSectionCompleted == false ? CurrentSectionName : NextSectionName;

    /// <summary>执行规格：工段未完工→CurrentSpec，已完工→CorrespondingSpec</summary>
    [JsonIgnore]
    public string? PendingSpec =>
        CurrentSectionCompleted == false ? CurrentSpec : CorrespondingSpec;

    /// <summary>在轧设备：工段未完工→CurrentEquipmentName ?? CurrentOutsource，已完工→null</summary>
    [JsonIgnore]
    public string? PendingEquipment =>
        CurrentSectionCompleted == false
            ? CurrentEquipmentName ?? CurrentOutsource
            : null;

    /// <summary>
    /// 重点生产批次（值已由 Service COALESCE：Plan ?? 系统值）：
    /// Tier 1：ScheduleStage==2 + 紧急 + pending条件
    /// Tier 2：ScheduleStage==1 + 催单/分批交货 + 紧急 + pending条件
    /// </summary>
    [JsonIgnore]
    public bool IsKeyBatch =>
        (ScheduleStage == 2 &&
         (UrgencyLevel == "A+急" || UrgencyLevel == "A急") &&
         (PendingProcess == "荒管处理" ||
          (PendingProcess == MainNoAttentionProcess) ||
          MainNoAttentionProcess is null or "收尾-成检"))
        ||
        (ScheduleStage == 1 &&
         (IsUrging || IsBatchDelivery) &&
         (UrgencyLevel == "A+急" || UrgencyLevel == "A急") &&
         (PendingProcess == "荒管处理" ||
          (PendingProcess == MainNoAttentionProcess) ||
          MainNoAttentionProcess is null or "收尾-成检"));

    // ===== G6：工单需求调整（来自 WorkOrderExecutionSummary 实体） =====
    public bool IsUrging { get; set; }
    public bool IsBatchDelivery { get; set; }
    public bool IsPaused { get; set; }
    public string? AdjustmentRemark { get; set; }

    // ===== G7：批次流转（计算字段，依赖冷轧排程匹配结果 + 紧急级别） =====

    private enum FlowTrigger { None, AttentionProcess, CompletionType, RollType }

    /// <summary>判定流转触发来源</summary>
    private FlowTrigger _trigger
    {
        get
        {
            // 生产关注工序=收尾-成检 → 始终流转
            if (MainNoAttentionProcess == "收尾-成检")
                return FlowTrigger.AttentionProcess;

            var isUrgent = UrgencyLevel == "A+急" || UrgencyLevel == "A急";
            var isPartial1 = isUrgent && (ScheduleStage == 2 || (ScheduleStage == 1 && (IsUrging || IsBatchDelivery)));
            var isPartial3 = isUrgent || UrgencyLevel == "B顺";

            // 在轧要求判定
            if (!string.IsNullOrEmpty(CR_CompletionType) && CR_CompletionType != "None")
            {
                if (CR_CompletionType == "All")
                    return FlowTrigger.CompletionType;
                if (CR_CompletionType == "Urgent" && IsKeyBatch)
                    return FlowTrigger.CompletionType;
                if (CR_CompletionType == "Partial1" && isPartial1)
                    return FlowTrigger.CompletionType;
                if (CR_CompletionType == "Partial2" && isUrgent)
                    return FlowTrigger.CompletionType;
                if (CR_CompletionType == "Partial3" && isPartial3)
                    return FlowTrigger.CompletionType;
            }

            // 待轧要求判定
            if (!string.IsNullOrEmpty(CR_RollType) && CR_RollType != "None")
            {
                if (CR_RollType == "All" || CR_RollType == "Subsequent")
                    return FlowTrigger.RollType;
                if (CR_RollType == "Urgent" && IsKeyBatch)
                    return FlowTrigger.RollType;
                if (CR_RollType == "Partial1" && isPartial1)
                    return FlowTrigger.RollType;
                if (CR_RollType == "Partial2" && isUrgent)
                    return FlowTrigger.RollType;
                if (CR_RollType == "Partial3" && isPartial3)
                    return FlowTrigger.RollType;
            }

            return FlowTrigger.None;
        }
    }

    /// <summary>
    /// 流转标注（UrgencyLevel 已由 Service COALESCE）：
    /// 生产关注工序=收尾-成检 → true
    /// 在轧要求=All → true；在轧要求=Urgent → true 仅当 IsKeyBatch
    /// 在轧要求=Partial1 → true 仅当 A+急/A急 且 (生产执行 或 原料锁定+催单/分批交货)
    /// 在轧要求=Partial2 → true 仅当 A+急/A急
    /// 在轧要求=Partial3 → true 仅当 A+急/A急/B顺
    /// 待轧要求=All/Subsequent → true；待轧要求=Urgent → true 仅当 IsKeyBatch
    /// 待轧要求=Partial1 → true 仅当 A+急/A急 且 (生产执行 或 原料锁定+催单/分批交货)
    /// 待轧要求=Partial2 → true 仅当 A+急/A急
    /// 待轧要求=Partial3 → true 仅当 A+急/A急/B顺
    /// </summary>
    [JsonIgnore]
    public bool IsFlow => _trigger != FlowTrigger.None;

    /// <summary>
    /// 流转等级：
    /// IsFlow=false → 3
    /// IsFlow=true + IsKeyBatch=true → 1
    /// IsFlow=true + IsKeyBatch=false → 2
    /// </summary>
    [JsonIgnore]
    public int FlowLevel
    {
        get
        {
            if (!IsFlow) return 3;
            return IsKeyBatch ? 1 : 2;
        }
    }

    /// <summary>流转目标</summary>
    [JsonIgnore]
    public string? FlowTarget => _trigger switch
    {
        FlowTrigger.AttentionProcess => "成检",
        FlowTrigger.CompletionType => "完工冷轧",
        FlowTrigger.RollType => "冷轧",
        _ => null,
    };

    /// <summary>待轧要求场景对应的层级冷轧工序类型</summary>
    private string? _rollTypeProcessType
    {
        get
        {
            if (!string.IsNullOrEmpty(CurrentCR_ProcessType) && string.IsNullOrEmpty(PendingEquipment))
                return CurrentCR_ProcessType;
            if (!string.IsNullOrEmpty(NextCR_ProcessType) && string.IsNullOrEmpty(PendingEquipment))
                return NextCR_ProcessType;
            if (!string.IsNullOrEmpty(NextNextCR_ProcessType) && string.IsNullOrEmpty(PendingEquipment))
                return NextNextCR_ProcessType;
            return null;
        }
    }

    /// <summary>待轧要求场景对应的层级轧制规格</summary>
    private string? _rollTypeRollingSpec
    {
        get
        {
            if (!string.IsNullOrEmpty(CurrentCR_ProcessType) && string.IsNullOrEmpty(PendingEquipment))
                return CurrentCR_RollingSpec;
            if (!string.IsNullOrEmpty(NextCR_ProcessType) && string.IsNullOrEmpty(PendingEquipment))
                return NextCR_RollingSpec;
            if (!string.IsNullOrEmpty(NextNextCR_ProcessType) && string.IsNullOrEmpty(PendingEquipment))
                return NextNextCR_RollingSpec;
            return null;
        }
    }

    /// <summary>冷轧类型</summary>
    [JsonIgnore]
    public string? FlowCRType => _trigger switch
    {
        FlowTrigger.AttentionProcess => "-",
        FlowTrigger.CompletionType => PendingProcess,
        FlowTrigger.RollType => _rollTypeProcessType,
        _ => null,
    };

    /// <summary>执行规格</summary>
    [JsonIgnore]
    public string? FlowExecSpec => _trigger switch
    {
        FlowTrigger.AttentionProcess => PendingSpec,
        FlowTrigger.CompletionType => CurrentCR_RollingSpec,
        FlowTrigger.RollType => _rollTypeRollingSpec,
        _ => null,
    };

    /// <summary>目标序（实时：根据 FlowTarget 从 ProcessGroups 推导）</summary>
    public int? TargetSequence { get; set; }

    // ===== G5：冷轧排程（后端从 ProcessGroups 推导 + 匹配冷轧小表） =====
    // G5-1：冷轧维度（由 ProcessGroups 推导）
    public string? CurrentCR_ProcessType { get; set; }   // 本层冷轧工序类型
    public string? CurrentCR_BilletSpec { get; set; }    // 本层冷轧坯料规格
    public string? CurrentCR_RollingSpec { get; set; }   // 本层冷轧轧制规格
    public bool CurrentCR_IsFinished { get; set; }       // 本层冷轧是否成品
    public string? NextCR_ProcessType { get; set; }      // 下层冷轧工序类型
    public string? NextCR_BilletSpec { get; set; }       // 下层冷轧坯料规格
    public string? NextCR_RollingSpec { get; set; }      // 下层冷轧轧制规格
    public bool NextCR_IsFinished { get; set; }          // 下层冷轧是否成品
    public string? NextNextCR_ProcessType { get; set; }  // 下下层冷轧工序类型
    public string? NextNextCR_BilletSpec { get; set; }   // 下下层冷轧坯料规格
    public string? NextNextCR_RollingSpec { get; set; }  // 下下层冷轧轧制规格
    public bool NextNextCR_IsFinished { get; set; }      // 下下层冷轧是否成品

    // G5-2：本层排程匹配结果（本层维度匹配冷轧小表）
    public string? CR_CompletionType { get; set; }       // 在轧要求

    // G5-3：下层排程匹配结果（下层维度匹配冷轧小表）
    public string? CR_RollType { get; set; }             // 待轧要求
    public int CR_RollOrder { get; set; }                // 顺序
    public string? CR_SchedMachineNo { get; set; }       // 待轧设备号

    // ===== 批次计划薄表（来自 BatchPlanSchedule 小表，持久化） =====
    public bool PlanIsFlow { get; set; }
    public int PlanFlowLevel { get; set; }
    public string? PlanFlowTarget { get; set; }
    public string? PlanFlowCRType { get; set; }
    public string? PlanFlowExecSpec { get; set; }
    public int? PlanExecutionSequence { get; set; }
    public int? PlanTargetSequence { get; set; }
    public bool IsGrabOrder { get; set; }
    public string? PlanRemark { get; set; }

    // ===== 执行反馈（实时计算） =====

    /// <summary>原工量差 = 小表目标序 - 小表执行序</summary>
    [JsonIgnore]
    public int? OriginalDiff =>
        PlanTargetSequence.HasValue && PlanExecutionSequence.HasValue
            ? PlanTargetSequence.Value - PlanExecutionSequence.Value
            : null;

    /// <summary>现工量差 = 小表目标序 - 当前执行序</summary>
    [JsonIgnore]
    public int? CurrentDiff =>
        PlanTargetSequence.HasValue && ExecutionSequence.HasValue
            ? PlanTargetSequence.Value - ExecutionSequence.Value
            : null;

    /// <summary>是否执行 = 原工量差 ≠ 现工量差</summary>
    [JsonIgnore]
    public bool? IsExecuted =>
        OriginalDiff.HasValue && CurrentDiff.HasValue
            ? OriginalDiff.Value != CurrentDiff.Value
            : null;

    /// <summary>达标 = 当前执行序 ≥ 小表目标序</summary>
    [JsonIgnore]
    public bool? IsCompliant =>
        ExecutionSequence.HasValue && PlanTargetSequence.HasValue
            ? ExecutionSequence.Value >= PlanTargetSequence.Value
            : null;
}
