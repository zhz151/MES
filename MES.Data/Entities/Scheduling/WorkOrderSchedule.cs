namespace MES.Data.Entities.Scheduling;

/// <summary>
/// 工单排程（物化表）
/// 通过"计划安排"按钮从 WorkOrderExecutionSummary 全量刷新。
/// 筛选规则：ScheduleStage=2 全部 + ScheduleStage=1(B已购未回且待回荒管重>0) +
/// ScheduleStage=1(销售催单且待回荒管重>0) + ScheduleStage=1(A质量影响)
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
    /// <summary>关注状态(0=无需排产 1=原料锁定 2=生产执行 3=成品检验)</summary>
    public int ScheduleStage { get; set; }

    /// <summary>剩余总工量（天）</summary>
    public int? TotalRemainingWorkDays { get; set; }

    /// <summary>工单计划性（A+急/A急/B顺/C缓/D缓）</summary>
    public string? UrgencyLevel { get; set; }

    /// <summary>工艺预计完成日</summary>
    public DateTime? EstimatedProcessCompletionDate { get; set; }

    /// <summary>交期相差天数</summary>
    public int? DaysDiffFromDelivery { get; set; }

    /// <summary>原锁备注</summary>
    public string? RawMaterialLockRemark { get; set; }

    // ========== G13: 销售催单 ==========
    public bool SalesUrging { get; set; }
    public string? UrgingRemark { get; set; }
}
