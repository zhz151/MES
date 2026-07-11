namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 工单排程 DTO
/// </summary>
public class WorkOrderScheduleDto
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }

    // ========== G1: 工单基础数据 ==========
    public string WorkOrderNo { get; set; } = null!;
    public string Salesman { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public DateTime SignDate { get; set; }
    public DateTime DeliveryDate { get; set; }
    public bool DelayPenalty { get; set; }
    public string SettlementMethod { get; set; } = null!;
    public string SalesOrderNo { get; set; } = null!;
    public string ProductionMainNo { get; set; } = null!;
    public string? ProductionSubNo { get; set; }
    public string MaterialName { get; set; } = null!;
    public string DeliveryState { get; set; } = null!;
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public string LengthStatus { get; set; } = null!;
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public int TotalItemCount { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalMeters { get; set; }
    public decimal TotalWeight { get; set; }

    // ========== G7: 有效流转 ==========
    public decimal FlowOutputRatio { get; set; }
    public int FlowStatus { get; set; }
    public decimal MainNoFlowOutputRatio { get; set; }
    public int MainNoFlowStatus { get; set; }
    public int FlowTotalBatchCount { get; set; }
    public int FlowIncompleteBatchCount { get; set; }
    public int FlowMaxRemainingWorkDays { get; set; }

    // ========== G12: 实时关注 ==========
    public int ScheduleStage { get; set; }
    public int? TotalRemainingWorkDays { get; set; }
    public int? CapacityWorkDays { get; set; }
    public string? UrgencyLevel { get; set; }
    public DateTime? EstimatedProcessCompletionDate { get; set; }
    public int? DaysDiffFromDelivery { get; set; }
    public string? RawMaterialLockRemark { get; set; }

    // ========== G13: 工单需求调整 ==========
    public bool IsUrging { get; set; }
    public bool IsBatchDelivery { get; set; }
    public bool IsPaused { get; set; }
    public string? AdjustmentRemark { get; set; }

    // ========== G14: 在产节点待量 ==========
    /// <summary>荒管处理·外抛光 待量(kg)</summary>
    public decimal? PendingSectionRoughTube { get; set; }

    /// <summary>在制修检·检验 待量(kg)</summary>
    public decimal? PendingSectionWarehouseFix { get; set; }

    /// <summary>60冷轧·冷轧拔 待量(kg)</summary>
    public decimal? PendingSection60Roll { get; set; }

    /// <summary>50冷轧·冷轧拔 待量(kg)</summary>
    public decimal? PendingSection50Roll { get; set; }

    /// <summary>30冷轧·冷轧拔 待量(kg)</summary>
    public decimal? PendingSection30Roll { get; set; }

    /// <summary>20冷轧·冷轧拔 待量(kg)</summary>
    public decimal? PendingSection20Roll { get; set; }

    /// <summary>三辊冷轧·冷轧拔 待量(kg)</summary>
    public decimal? PendingSectionThreeRoll { get; set; }

    /// <summary>冷拔·冷轧拔 待量(kg)</summary>
    public decimal? PendingSectionDrawBench { get; set; }

    /// <summary>变形工序是否完成</summary>
    public bool DeformedProcessCompleted { get; set; }

    /// <summary>生产关注工序</summary>
    public string? ProductionAttentionProcess { get; set; }

    /// <summary>生产流转性</summary>
    public string? ProductionFlowProperty { get; set; }

    /// <summary>最大剩余工量（天）：此工单号下所有批次中 RemainingWorkDays 的最大值</summary>
    public int? MaxBatchRemainingWorkDays { get; set; }

    /// <summary>主号关注工序：同主号下剩余工量最大值所在工单的生产关注工序</summary>
    public string? MainNoAttentionProcess { get; set; }

    // ========== G15: 工单计划薄表覆盖值（null = 使用系统值）==========
    /// <summary>工单状态覆盖</summary>
    public int? PlanScheduleStage { get; set; }

    /// <summary>紧急性覆盖</summary>
    public string? PlanUrgencyLevel { get; set; }

    /// <summary>生产关注工序覆盖</summary>
    public string? PlanProductionAttentionProcess { get; set; }

    /// <summary>生产流转性覆盖</summary>
    public string? PlanProductionFlowProperty { get; set; }

    /// <summary>显示的工单状态（覆盖值 ?? 系统值）</summary>
    public int DisplayScheduleStage => PlanScheduleStage ?? ScheduleStage;

    /// <summary>显示的紧急性（覆盖值 ?? 系统值）</summary>
    public string? DisplayUrgencyLevel => PlanUrgencyLevel ?? UrgencyLevel;

    /// <summary>显示的生产关注工序（覆盖值 ?? 系统值）</summary>
    public string? DisplayProductionAttentionProcess => PlanProductionAttentionProcess ?? MainNoAttentionProcess;

    /// <summary>显示的生产流转性（覆盖值 ?? 系统值）</summary>
    public string? DisplayProductionFlowProperty => PlanProductionFlowProperty ?? ProductionFlowProperty;

    // ========== 实时一致性 ==========
    /// <summary>
    /// 实时一致性（由 Service 在查询后设置）：
    /// - "一致"：4 个 Plan 字段均匹配系统值
    /// - "进度调整"：仅 ProductionAttentionProcess 不一致（人为调进度，合理）
    /// - "值存疑"：工单状态/紧急性/流转性 任一不一致（存在疑问）
    /// - "错误"：同主号下不同工单的计划值不一致（应保持主号级一致）
    /// </summary>
    public string? ConsistencyStatus { get; set; }

    // ========== 显示文本 ==========
    public string DelayPenaltyText => DelayPenalty ? "是" : "否";
    public string ScheduleStageText => ScheduleStage switch
    {
        0 => "工单完成",
        1 => "原料锁定",
        2 => "生产执行",
        3 => "成品检验",
        _ => "未知"
    };
    public string UrgingText => IsUrging ? "是" : "否";
}
