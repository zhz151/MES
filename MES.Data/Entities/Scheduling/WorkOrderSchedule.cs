namespace MES.Data.Entities.Scheduling;

/// <summary>
/// 工单排程（物化表）
/// 通过"计划安排"按钮从 WorkOrderExecutionSummary 全量刷新。
/// 筛选规则：
///   块1: ScheduleStage=2（生产执行）全部
///   块2: RawMaterialLockPlanAndExecution 中 IsMainNoMaterialComplete=true 的 (ProductionMainNo,SalesOrderNo) 下所有工单
///   块3: ScheduleStage=1（原料锁定）且催单(IsUrging=true)且分批交货(IsBatchDelivery=true)的工单
/// </summary>
public class WorkOrderSchedule : BaseEntity
{
    // ========== 工单标识 ==========
    /// <summary>工单ID（唯一，一个工单一条记录）</summary>
    public int WorkOrderId { get; set; }

    /// <summary>工单号</summary>
    public string WorkOrderNo { get; set; } = null!;

    // ========== G1: 工单基础数据 ==========
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

    // ========== G12: 实时关注（来自 WorkOrderExecutionSummary） ==========
    /// <summary>关注状态(0=工单完成 1=原料锁定 2=生产执行 3=成品检验)</summary>
    public int ScheduleStage { get; set; }

    /// <summary>剩余总工量（天）</summary>
    public int? TotalRemainingWorkDays { get; set; }

    /// <summary>产能工量（天）</summary>
    public int? CapacityWorkDays { get; set; }

    /// <summary>工单计划性（A+急/A急/B顺/C缓/D缓）</summary>
    public string? UrgencyLevel { get; set; }

    /// <summary>工艺预计完成日</summary>
    public DateTime? EstimatedProcessCompletionDate { get; set; }

    /// <summary>交期相差天数</summary>
    public int? DaysDiffFromDelivery { get; set; }

    /// <summary>原锁备注</summary>
    public string? RawMaterialLockRemark { get; set; }

    // ========== G13: 工单需求调整 ==========
    public bool IsUrging { get; set; }
    public bool IsBatchDelivery { get; set; }
    public bool IsPaused { get; set; }
    public string? AdjustmentRemark { get; set; }

    // ========== G14: 在产节点待量（来自 WorkOrderExecutionSummary） ==========
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
}
