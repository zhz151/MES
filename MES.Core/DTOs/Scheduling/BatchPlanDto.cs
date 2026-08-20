using System.Text.Json.Serialization;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 在产明细计划 DTO — ProductionBatch LEFT JOIN WorkOrderExecutionSummary + WorkOrderPlan
/// </summary>
public class BatchPlanDto
{
    // ===== 内部字段 =====
    public int BatchId { get; set; }

    // ===== G1：批次信息 =====
    public string BatchNo { get; set; } = string.Empty;
    public string? TagNo { get; set; }
    public string PlantGrade { get; set; } = string.Empty;
    public int? CurrentValidWeight { get; set; }

    // ===== G2：关联工单信息 =====
    public string WorkOrderNo { get; set; } = string.Empty;
    public string? SalesOrderNo { get; set; }
    public string? ProductionMainNo { get; set; }
    public string? EndCustomer { get; set; }
    public string? Salesman { get; set; }
    public DateTime DeliveryDate { get; set; }
    public DeliveryState? DeliveryState { get; set; }
    public string Specification { get; set; } = string.Empty;
    /// <summary>制造物品（产类判定输入，供荒管检/在制检按产类过滤）</summary>
    public string? ManufacturingItem { get; set; }
    /// <summary>生产类型（荒管生产/在制生产/库存/外购/返整/委外生产/对外加工）</summary>
    public string? ProductionType { get; set; }
    /// <summary>制造状态（批次实际制造状态，与交货状态同枚举）</summary>
    public string? ManufacturingStatus { get; set; }
    public LengthStatus? LengthStatus { get; set; }
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

    // ===== G4：工单计划（COALESCE：工单计划薄表优先，无覆盖则回退系统值） =====
    public string? UrgencyLevel { get; set; }
    public int ScheduleStage { get; set; }
    public string? MainNoAttentionProcess { get; set; }
    /// <summary>相应工段序：根据主号关注工序从 ProcessGroups 推导的工段序号（Inspection 或 ColdRollDraw）</summary>
    public int? AttentionProcessSectionSequence { get; set; }
    public string? ProductionFlowProperty { get; set; }

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

    /// <summary>在产单位：工段未完工→CurrentOutsource（当前委外），已完工→null</summary>
    public string? PendingUnit =>
        CurrentSectionCompleted == false
            ? CurrentOutsource
            : null;

    /// <summary>在产设备：工段未完工→CurrentEquipmentName（当前设备），已完工→null</summary>
    public string? PendingEquipment =>
        CurrentSectionCompleted == false
            ? CurrentEquipmentName
            : null;

    /// <summary>是否在轧（在产单位或在产设备任一非空），仅后端排程判定用，前端不显示</summary>
    [JsonIgnore]
    public bool IsProducing =>
        CurrentSectionCompleted == false
            && (!string.IsNullOrEmpty(CurrentEquipmentName) || !string.IsNullOrEmpty(CurrentOutsource));

    /// <summary>
    /// 重点生产批次（Service 计算字段，G4 工单计划列显示用，仅工单计划概念，冷轧排程逻辑不再使用）：
    /// 前置条件：UrgencyLevel==A+急/A急 + ProductionFlowProperty==正常 + MainNoAttentionProcess非空
    ///   生产收尾（变形工序已完成，与成品检验衔接）→ 直接重点（不要求序号比较）
    ///   其余：ExecutionSequence 与 AttentionProcessSectionSequence 序号比较（未产批次执行序视为 0）：
    ///   冷轧类(含三辊冷轧/冷拔)：ExecutionSequence &lt; AttentionProcessSectionSequence + 1
    ///   其他(荒管/在制修检/收尾成检)：ExecutionSequence &lt; AttentionProcessSectionSequence
    /// </summary>
    public bool IsKeyBatch { get; set; }

    /// <summary>
    /// 关注工序==当前冷轧排程行（Service 计算字段，Model B 特急档判定输入）：
    /// 主号关注工序（MainNoAttentionProcess）与批次命中的冷轧排程行 ProcessType（ProcessKeys 归一）相等
    /// </summary>
    public bool AttentionMatchesCurrentCR { get; set; }

    // ===== G6：工单需求调整（来自 WorkOrderExecutionSummary 实体） =====
    public bool IsUrging { get; set; }
    public bool IsBatchDelivery { get; set; }
    public bool IsPaused { get; set; }
    public string? AdjustmentRemark { get; set; }

    // ===== G7：关联冷轧排程（计算字段，依赖冷轧排程匹配结果 + 紧急级别） =====

    private enum FlowTrigger { None, CompletionType, RollType }

    /// <summary>判定流转触发来源（仅与冷轧排程档位关联；无档位匹配 → 不流转）</summary>
    private FlowTrigger _trigger
    {
        get
        {
            var isUrgent = UrgencyLevelKeys.IsUrgent(UrgencyLevel);
            var isNormal = ProductionFlowProperty == ProductionFlowKeys.Normal;
            var isPartial3 = isUrgent || UrgencyLevel == UrgencyLevelKeys.BOrder;

            // 在轧要求判定（档位语义与冷轧排程 MatchesScheduleType 一致，Model B；
            // ⚠️ Subsequent=All 全量、Partial1=Urgent 为 V5.25 历史档位，存量数据可能存有，须兼容）
            if (!string.IsNullOrEmpty(CR_CompletionType) && CR_CompletionType != "None")
            {
                if (CR_CompletionType == "All" || CR_CompletionType == "Subsequent")
                    return FlowTrigger.CompletionType;
                if (CR_CompletionType == "CrOnly" && isUrgent && isNormal && AttentionMatchesCurrentCR)
                    return FlowTrigger.CompletionType;
                if ((CR_CompletionType == "Urgent" || CR_CompletionType == "Partial1") && isUrgent && isNormal)
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
                if (CR_RollType == "CrOnly" && isUrgent && isNormal && AttentionMatchesCurrentCR)
                    return FlowTrigger.RollType;
                if ((CR_RollType == "Urgent" || CR_RollType == "Partial1") && isUrgent && isNormal)
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
    /// 流转标注（仅与冷轧排程档位关联，与冷轧计划「排程选中」口径一致，二值 是/否，Model B）：
    /// 在轧要求=All → true；在轧要求=CrOnly → true 仅当 特急(正常流转∧关注==当前冷轧)
    /// 在轧要求=Urgent → true 仅当 特急/特急-(正常流转)；在轧要求=Partial2 → true 仅当 A+急/A急
    /// 在轧要求=Partial3 → true 仅当 A+急/A急/B顺
    /// 待轧要求=All → true；待轧要求=CrOnly → true 仅当 特急(正常流转∧关注==当前冷轧)
    /// 待轧要求=Urgent → true 仅当 特急/特急-(正常流转)；待轧要求=Partial2 → true 仅当 A+急/A急
    /// 待轧要求=Partial3 → true 仅当 A+急/A急/B顺
    /// 重点兜底（V5.35）：重点生产批次且冷轧排程未命中 → 按主号关注工序兜底流转（IsFlow=true）
    /// </summary>
    [JsonIgnore]
    public bool IsFlow => _trigger != FlowTrigger.None || KeyBatchFallback;

    /// <summary>重点兜底判定（V5.35 用户决策：实时也加重点兜底，与薄表规则(2) 一致）：重点生产批次且冷轧排程未命中 → 按主号关注工序兜底流转</summary>
    [JsonIgnore]
    public bool KeyBatchFallback => IsKeyBatch && _trigger == FlowTrigger.None;

    /// <summary>重点兜底执行规格（V5.35 Service 填充：收尾→待产规格、其余→主号关注工序对应工序组规格；仅 KeyBatchFallback 时消费）</summary>
    [JsonIgnore]
    public string? KeyBatchFallbackExecSpec { get; set; }

    /// <summary>
    /// 排程档位（批次实际档位，V5.26 细化，与 _trigger/冷轧排程口径一致，档位序 急+&gt;急&gt;急-&gt;顺&gt;带&gt;略）：
    /// 1=急+（正常流转∧关注==当前冷轧）、2=急（正常流转∧关注≠当前冷轧）、3=急-（非正常流转）
    /// 4=顺（非急但流转，B顺）、5=带（All 档下非急非顺的普通批次）、6=略（不在排程内：无排程行/被要求档排除，IsFlow=false）
    /// </summary>
    [JsonIgnore]
    public int ScheduleTier
    {
        get
        {
            if (!IsFlow) return 6;
            if (KeyBatchFallback) return 2; // V5.35 重点兜底 → 急（与薄表 MapScheduleTierToPlanLevel(2)=2 一致）
            var isUrgent = UrgencyLevelKeys.IsUrgent(UrgencyLevel);
            var isNormal = ProductionFlowProperty == ProductionFlowKeys.Normal;
            if (isUrgent && isNormal && AttentionMatchesCurrentCR) return 1;
            if (isUrgent && isNormal) return 2;
            if (isUrgent) return 3;
            if (UrgencyLevel == UrgencyLevelKeys.BOrder) return 4;
            return 5;
        }
    }

    /// <summary>排程档位显示文本：急+/急/急-/顺/带/略</summary>
    [JsonIgnore]
    public string ScheduleTierDisplay => ScheduleTier switch
    {
        1 => "急+",
        2 => "急",
        3 => "急-",
        4 => "顺",
        5 => "带",
        _ => "略",
    };

    /// <summary>流转目标</summary>
    public string? FlowTarget => _trigger switch
    {
        FlowTrigger.CompletionType => FlowTargetKeys.CompletionColdRoll,
        FlowTrigger.RollType => FlowTargetKeys.ColdRoll,
        _ => KeyBatchFallback ? MapFlowTargetByCRType(MainNoAttentionProcess) : null, // V5.35 重点兜底（与薄表规则(2) 一致）
    };

    /// <summary>
    /// 待轧要求场景对应的层级冷轧工序类型（V5.33 用户决策：优先=「冷轧排程(实时)」物理下一冷轧拔层，
    /// 回退=本层→下层→下下层）。排程匹配层不再作为显示层，仅用于 IsFlow/机台判断（匹配层可通过 CR_RollType/CR_SchedMachineNo 识别）。
    /// V5.35 在轧对齐：本层冷轧拔已完工且本层检验/酸洗在轧时也走待轧匹配（_trigger=RollType）——不再以 PendingEquipment 判断，
    /// 因 FlowCRType 的 _trigger switch 已保证仅在 RollType 分支调用本属性（在轧匹配走 CompletionType 分支、不调用）。
    /// </summary>
    private string? _rollTypeProcessType
    {
        get
        {
            if (!string.IsNullOrEmpty(RealTimeCR_ProcessType))
                return RealTimeCR_ProcessType;
            // 回退（防御）：实时组未计算的边界场景，回退本层→下层→下下层
            if (!string.IsNullOrEmpty(CurrentCR_ProcessType))
                return CurrentCR_ProcessType;
            if (!string.IsNullOrEmpty(NextCR_ProcessType))
                return NextCR_ProcessType;
            if (!string.IsNullOrEmpty(NextNextCR_ProcessType))
                return NextNextCR_ProcessType;
            return null;
        }
    }

    /// <summary>待轧要求场景对应的层级轧制规格（优先=「冷轧排程(实时)」物理下一冷轧拔层，回退=本层→下层→下下层）</summary>
    private string? _rollTypeRollingSpec
    {
        get
        {
            if (!string.IsNullOrEmpty(RealTimeCR_RollingSpec))
                return RealTimeCR_RollingSpec;
            if (!string.IsNullOrEmpty(CurrentCR_ProcessType))
                return CurrentCR_RollingSpec;
            if (!string.IsNullOrEmpty(NextCR_ProcessType))
                return NextCR_RollingSpec;
            if (!string.IsNullOrEmpty(NextNextCR_ProcessType))
                return NextNextCR_RollingSpec;
            return null;
        }
    }

    /// <summary>待轧要求场景对应的层级坯料规格（优先=「冷轧排程(实时)」物理下一冷轧拔层，回退=本层→下层→下下层）</summary>
    private string? _rollTypeBilletSpec
    {
        get
        {
            if (!string.IsNullOrEmpty(RealTimeCR_BilletSpec))
                return RealTimeCR_BilletSpec;
            if (!string.IsNullOrEmpty(CurrentCR_ProcessType))
                return CurrentCR_BilletSpec;
            if (!string.IsNullOrEmpty(NextCR_ProcessType))
                return NextCR_BilletSpec;
            if (!string.IsNullOrEmpty(NextNextCR_ProcessType))
                return NextNextCR_BilletSpec;
            return null;
        }
    }

    /// <summary>冷轧类型</summary>
    public string? FlowCRType => _trigger switch
    {
        FlowTrigger.CompletionType => PendingProcess,
        FlowTrigger.RollType => _rollTypeProcessType,
        _ => KeyBatchFallback ? MainNoAttentionProcess : null, // V5.35 重点兜底（与薄表规则(2) planFlowCRType 一致）
    };

    /// <summary>外径跨度 — 坯料外径-轧制外径，如"110-89"（重点兜底无外径跨度，与薄表规则(2) planOuterDiameterSpan=null 一致）</summary>
    public string? OuterDiameterSpan => _trigger switch
    {
        FlowTrigger.CompletionType => GetShortDisplay(CurrentCR_BilletSpec, CurrentCR_RollingSpec),
        FlowTrigger.RollType => GetShortDisplay(_rollTypeBilletSpec, _rollTypeRollingSpec),
        _ => null,
    };

    /// <summary>执行规格</summary>
    public string? FlowExecSpec => _trigger switch
    {
        FlowTrigger.CompletionType => CurrentCR_RollingSpec,
        FlowTrigger.RollType => _rollTypeRollingSpec,
        _ => KeyBatchFallback ? KeyBatchFallbackExecSpec : null, // V5.35 重点兜底（Service 填充）
    };

    /// <summary>目标序（实时：根据 FlowTarget 从 ProcessGroups 推导）</summary>
    public int? TargetSequence { get; set; }

    // ===== G5：冷轧排程（后端从 ProcessGroups 推导 + 匹配冷轧小表） =====
    // G5-1：冷轧维度（由 ProcessGroups 推导）
    public string? CurrentCR_ProcessType { get; set; }   // 本层冷轧工序类型
    public string? CurrentCR_BilletSpec { get; set; }    // 本层冷轧坯料规格
    public string? CurrentCR_RollingSpec { get; set; }   // 本层冷轧轧制规格
    public bool CurrentCR_IsFinished { get; set; }       // 本层冷轧是否成品
    /// <summary>
    /// 变形序完成（本层冷轧拔工段是否已轧过，V5.32 用户决策）：
    /// true=完成（本层冷轧拔已轧过/本层无冷轧拔默认完成）、false=否（未轧过）、null=本层非冷轧（视为完成）。
    /// 与 V5.31 IsColdRollPassDone / 排程侧 BuildAllocationsAsync 同口径（跨组：层在当前工序组之前=已过，之后=未到）。
    /// </summary>
    public bool? CurrentCR_DeformedSeqCompleted { get; set; }
    public string? NextCR_ProcessType { get; set; }      // 下层冷轧工序类型
    public string? NextCR_BilletSpec { get; set; }       // 下层冷轧坯料规格
    public string? NextCR_RollingSpec { get; set; }      // 下层冷轧轧制规格
    public bool NextCR_IsFinished { get; set; }          // 下层冷轧是否成品
    public string? NextNextCR_ProcessType { get; set; }  // 下下层冷轧工序类型
    public string? NextNextCR_BilletSpec { get; set; }   // 下下层冷轧坯料规格
    public string? NextNextCR_RollingSpec { get; set; }  // 下下层冷轧轧制规格
    public bool NextNextCR_IsFinished { get; set; }      // 下下层冷轧是否成品

    // G5-4：冷轧排程(实时)（批次的「下一个冷轧拔层」规格信息，V5.32 用户决策）
    // 取值：本层变形序未完成（或本层非冷轧/无冷轧拔=默认完成）→ 本层；已完成 → 下层（有数据）→ 否则下下层。
    // 与 V5.31 待轧分支「找到第一个冷轧拔未完成的层」/ 排程侧 BuildAllocationsAsync 口径一致。
    public string? RealTimeCR_ProcessType { get; set; }  // 实时冷轧工序
    public string? RealTimeCR_BilletSpec { get; set; }   // 实时来料规格
    public string? RealTimeCR_RollingSpec { get; set; }  // 实时在轧规格
    public bool RealTimeCR_IsFinished { get; set; }      // 实时末道

    // G5-2：本层排程匹配结果（本层维度匹配冷轧小表）
    public string? CR_CompletionType { get; set; }       // 在轧要求

    // G5-3：下层排程匹配结果（下层维度匹配冷轧小表）
    public string? CR_RollType { get; set; }             // 待轧要求
    public string? CR_SchedMachineNo { get; set; }       // 待轧设备号

    // ===== 批次计划薄表（来自 BatchPlanSchedule 小表，持久化） =====
    /// <summary>暂停（控制开关）：=是 时流转字段读时覆盖为非流转（PlanIsFlow=false/PlanFlowLevel=5/流转位等=null），DB 保留原值，切回"否"自动恢复</summary>
    public bool PlanIsPaused { get; set; }
    public bool PlanIsFlow { get; set; }
    public int PlanFlowLevel { get; set; }

    /// <summary>
    /// PlanFlowLevel 显示文本（V5.28 五档，与实时排程档位映射：急+→急+/急→急/急-→急-/顺·带→一般/略→略）：
    /// 1=急+ / 2=急 / 3=急- / 4=一般 / 5=略（特急A/B 手工档已删除，急+ 直接透传实时档位）
    /// </summary>
    [JsonIgnore]
    public string PlanFlowLevelDisplay => PlanFlowLevel switch
    {
        1 => "急+",
        2 => "急",
        3 => "急-",
        4 => "一般",
        5 => "略",
        _ => PlanFlowLevel.ToString(),
    };
    public string? PlanFlowTarget { get; set; }
    public string? PlanFlowCRType { get; set; }
    public string? PlanOuterDiameterSpan { get; set; }
    public string? PlanFlowExecSpec { get; set; }
    public int? PlanExecutionSequence { get; set; }
    public int? PlanTargetSequence { get; set; }
    public bool IsGrabOrder { get; set; }
    public string? PlanRemark { get; set; }

    // ===== 执行反馈（实时计算） =====

    /// <summary>原工量差 = 小表目标序 - 小表执行序（未产执行序视为 0）</summary>
    public int OriginalDiff =>
        (PlanTargetSequence ?? 0) - (PlanExecutionSequence ?? 0);

    /// <summary>现工量差 = 小表目标序 - 当前执行序（未产执行序视为 0）</summary>
    public int CurrentDiff =>
        (PlanTargetSequence ?? 0) - (ExecutionSequence ?? 0);

    /// <summary>是否执行 = 原工量差 ≠ 现工量差（执行序是否推进，未产视为未执行）</summary>
    public bool IsExecuted => OriginalDiff != CurrentDiff;

    /// <summary>
    /// 达标状态（与批次计划关联）：批次计划流转=否 → null("-")；
    /// 流转=是 → 现工量差 &lt;= 0 即达标，否则未达标（取消"半达标"概念，只有 达标/未达标/-）
    /// </summary>
    public string? IsCompliant
    {
        get
        {
            if (!PlanIsFlow) return null;
            return CurrentDiff <= 0 ? "达标" : "未达标";
        }
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

    /// <summary>重点兜底流转目标（V5.35，与薄表 BatchPlanScheduleService.MapFlowTargetByCRType 同源）：
    /// 荒管处理→荒管检、在制修检→在制检、生产收尾→成品检验、空→null、剩余（冷轧类工序）→冷轧</summary>
    private static string? MapFlowTargetByCRType(string? crType)
    {
        if (string.IsNullOrEmpty(crType)) return null;
        if (crType == ProductionAttentionKeys.Finish) return FlowTargetKeys.FinalCheck;
        var key = ProcessKeys.ToKey(crType) ?? crType;
        if (key == ProcessKeys.RoughTubeProcessing) return FlowTargetKeys.RoughTubeCheck;
        if (key == ProcessKeys.InProcessRepair) return FlowTargetKeys.InProcessCheck;
        return FlowTargetKeys.ColdRoll;
    }
}
