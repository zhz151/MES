namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 原锁计划 DTO（G1+G2+G5+G3+G7+G10+G12+G13+G15）
/// </summary>
public class RawMaterialLockPlanAndExecutionDto
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

    // ========== G2: 用料计划 ==========
    public DateTime? LatestPlanDate { get; set; }
    public decimal MaterialPlanRate { get; set; }
    public int MaterialPlanStatus { get; set; }
    public decimal MainNoMaterialPlanRate { get; set; }
    public int MainNoMaterialPlanStatus { get; set; }
    public int ProcessCycle { get; set; }

    /// <summary>用料占比：4种料态中有做计划的种数(0-4)</summary>
    public int MaterialPlanCoveredCount { get; set; }

    /// <summary>用料占比文本：如"穿105% 荒160% 成20% 库40%"</summary>
    public string? MaterialPlanProportion { get; set; }

    /// <summary>要求到货日（最晚）</summary>
    public DateTime? LatestRequiredDate { get; set; }

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
    public int? CapacityWorkDays { get; set; }
    public string? UrgencyLevel { get; set; }
    public DateTime? EstimatedProcessCompletionDate { get; set; }
    public int? DaysDiffFromDelivery { get; set; }
    public string? RawMaterialLockRemark { get; set; }

    // ========== G13: 工单需求调整（从 WorkOrderExecutionSummary 实体读取） ==========
    public bool IsUrging { get; set; }
    public bool IsBatchDelivery { get; set; }
    public bool IsPaused { get; set; }
    public string? AdjustmentRemark { get; set; }

    // ========== G15: 预执行（页面操作标记）==========
    /// <summary>执行：近几日会投料</summary>
    public bool IsPreInput { get; set; }

    /// <summary>预算主号齐全：用户手动强制标记</summary>
    public bool IsBudgetComplete { get; set; }

    /// <summary>预算投料日</summary>
    public DateTime? BudgetInputDate { get; set; }

    /// <summary>执行错误：已执行且（日期为空 或 日期早于今天）</summary>
    public bool ExecutionError => IsPreInput && (!BudgetInputDate.HasValue || BudgetInputDate.Value.Date < DateTime.Today);

    /// <summary>主号齐全：系统计算</summary>
    public bool IsMainNoMaterialComplete { get; set; }

    /// <summary>是否存在异常（逾期、锁定未齐全等）</summary>
    public bool HasAbnormality { get; set; }

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
    public string IsPreInputText => IsPreInput ? "是" : "否";
    public string BudgetInputDateText => BudgetInputDate?.ToString("yyyy-MM-dd") ?? "-";
    public string ExecutionErrorText => ExecutionError ? "是" : "否";
    public string IsMainNoMaterialCompleteText => IsMainNoMaterialComplete ? "是" : "否";
}
