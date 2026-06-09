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

    // ===== G4：批次关注（COALESCE：工单计划薄表优先，无覆盖则回退系统值） =====
    public string? UrgencyLevel { get; set; }
    public int ScheduleStage { get; set; }
    public string? ProductionAttentionProcess { get; set; }
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
          (PendingProcess == ProductionAttentionProcess) ||
          ProductionAttentionProcess is null or "收尾-成检"))
        ||
        (ScheduleStage == 1 &&
         (IsUrging || IsBatchDelivery) &&
         (UrgencyLevel == "A+急" || UrgencyLevel == "A急") &&
         (PendingProcess == "荒管处理" ||
          (PendingProcess == ProductionAttentionProcess) ||
          ProductionAttentionProcess is null or "收尾-成检"));

    // ===== G6：工单需求调整（来自 WorkOrderExecutionSummary 实体） =====
    public bool IsUrging { get; set; }
    public bool IsBatchDelivery { get; set; }
    public bool IsPaused { get; set; }
    public string? AdjustmentRemark { get; set; }

    // ===== G7：批次流转（计算字段，依赖冷轧排程匹配结果 + 紧急级别） =====

    /// <summary>
    /// 流转标注（UrgencyLevel 已由 Service COALESCE）：
    /// 在轧要求=All/Partial → true；在轧要求=Urgent → true 仅当 UrgencyLevel 为 A+急/A急
    /// 待轧要求=All/Subsequent/Partial → true；待轧要求=Urgent → true 仅当 UrgencyLevel 为 A+急/A急
    /// </summary>
    [JsonIgnore]
    public bool IsFlow
    {
        get
        {
            var isUrgent = UrgencyLevel == "A+急" || UrgencyLevel == "A急";

            // 在轧要求判定
            if (!string.IsNullOrEmpty(CR_CompletionType) && CR_CompletionType != "None")
            {
                if (CR_CompletionType == "All" || CR_CompletionType == "Partial")
                    return true;
                if (CR_CompletionType == "Urgent" && isUrgent)
                    return true;
            }

            // 待轧要求判定
            if (!string.IsNullOrEmpty(CR_RollType) && CR_RollType != "None")
            {
                if (CR_RollType == "All" || CR_RollType == "Subsequent" || CR_RollType == "Partial")
                    return true;
                if (CR_RollType == "Urgent" && isUrgent)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 流转等级：
    /// IsFlow=true 且 (IsUrging=true 或 IsKeyBatch=true) → 1
    /// IsFlow=true 且 不满足上述条件 → 2
    /// IsFlow=false → 3
    /// </summary>
    [JsonIgnore]
    public int FlowLevel
    {
        get
        {
            if (!IsFlow) return 3;
            return IsUrging || IsKeyBatch ? 1 : 2;
        }
    }

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
}
