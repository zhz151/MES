namespace MES.Data.Entities.Scheduling;

/// <summary>
/// 原锁计划及执行（物化表，通过"计划安排"按钮全量刷新）
/// 存储 ScheduleStage=1（原料锁定）的工单快照数据
/// </summary>
public class RawMaterialLockPlanAndExecution : BaseEntity
{
    /// <summary>工单ID（唯一）</summary>
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

    // ========== G2: 用料计划 ==========
    public DateTime? LatestPlanDate { get; set; }
    public decimal MaterialPlanRate { get; set; }
    public int MaterialPlanStatus { get; set; }
    public decimal MainNoMaterialPlanRate { get; set; }
    public int MainNoMaterialPlanStatus { get; set; }
    public int ProcessCycle { get; set; }

    // ========== G5: 物料执行实时信息 ==========
    public int PendingRoughTubeQty { get; set; }
    public decimal PendingRoughTubeWeight { get; set; }
    public int PendingOutsourceFinishQty { get; set; }
    public decimal PendingOutsourceFinishWeight { get; set; }
    public decimal TheoreticalFinishQty { get; set; }
    public decimal TheoreticalFinishWeight { get; set; }

    // ========== G3: 投料数据 ==========
    public DateTime? InputStartDate { get; set; }
    public DateTime? InputEndDate { get; set; }
    public int TotalBatchCount { get; set; }
    public int InputQuantity { get; set; }
    public decimal InputWeight { get; set; }
    public decimal TheoreticalOutputQty { get; set; }
    public decimal TheoreticalOutputWeight { get; set; }
    public decimal InputOutputRatio { get; set; }
    public int InputStatus { get; set; }
    public decimal MainNoInputOutputRatio { get; set; }
    public int MainNoInputStatus { get; set; }

    // ========== G7: 有效流转 ==========
    public decimal FlowOutputRatio { get; set; }
    public int FlowStatus { get; set; }
    public decimal MainNoFlowOutputRatio { get; set; }
    public int MainNoFlowStatus { get; set; }
    public int FlowTotalBatchCount { get; set; }
    public int FlowIncompleteBatchCount { get; set; }
    public int FlowMaxRemainingWorkDays { get; set; }

    // ========== G10: 汇总不合格 ==========
    public decimal GeneralDefectWeight { get; set; }
    public decimal GeneralDefectRatio { get; set; }
    public decimal SeriousDefectWeight { get; set; }
    public decimal SeriousDefectRatio { get; set; }
    public decimal ScrapWeight { get; set; }
    public decimal ScrapRatio { get; set; }

    // ========== G12: 实时关注 ==========
    public int ScheduleStage { get; set; }
    public int? TotalRemainingWorkDays { get; set; }
    public string? UrgencyLevel { get; set; }
    public DateTime? EstimatedProcessCompletionDate { get; set; }
    public int? DaysDiffFromDelivery { get; set; }
    public string? RawMaterialLockRemark { get; set; }

    // ========== G13: 销售催单（从 SalesUrging 实体 JOIN 获取） ==========
    /// <summary>销售催单</summary>
    public bool SalesUrging { get; set; }

    /// <summary>催单备注</summary>
    public string? UrgingRemark { get; set; }

    // ========== G14: 执行快照 ==========
    /// <summary>现关注：最新的关注状态（执行数据更新时刷新）</summary>
    public int? CurrentScheduleStage { get; set; }

    /// <summary>现原锁关注：最新的原锁备注（执行数据更新时刷新）</summary>
    public string? CurrentRawMaterialLockRemark { get; set; }

    /// <summary>是否执行：CurrentRawMaterialLockRemark <> RawMaterialLockRemark 时为 true</summary>
    public bool? IsExecuted { get; set; }

    // ========== 看板筛选 - 异常标记 ==========
    /// <summary>是否存在异常（交期逾期/锁定未齐全/负工量等）</summary>
    public bool HasAbnormality { get; set; }
}
