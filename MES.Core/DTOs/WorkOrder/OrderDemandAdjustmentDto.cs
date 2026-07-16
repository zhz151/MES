using MES.Core.Enums;

namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 工单需求调整列表 DTO（G1+G12 + 手工字段）
/// </summary>
public class OrderDemandAdjustmentDto
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
    public SettlementMethod SettlementMethod { get; set; }
    public string SalesOrderNo { get; set; } = null!;
    public string ProductionMainNo { get; set; } = null!;
    public string? ProductionSubNo { get; set; }
    public string MaterialName { get; set; } = null!;
    public DeliveryState DeliveryState { get; set; }
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public LengthStatus LengthStatus { get; set; }
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public int TotalItemCount { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalMeters { get; set; }
    public decimal TotalWeight { get; set; }

    // ========== G12: 实时关注 ==========
    public int ScheduleStage { get; set; }
    public int? TotalRemainingWorkDays { get; set; }
    public int? CapacityWorkDays { get; set; }
    public string? UrgencyLevel { get; set; }
    public DateTime? EstimatedProcessCompletionDate { get; set; }
    public int? DaysDiffFromDelivery { get; set; }
    public string? RawMaterialLockRemark { get; set; }

    // ========== G7: 有效流转 ==========
    /// <summary>流转成品比(%)</summary>
    public decimal FlowOutputRatio { get; set; }

    /// <summary>有效流转状态(0=未投料 1=部分 2=满足)</summary>
    public int FlowStatus { get; set; }

    /// <summary>有效主号流转比(%)</summary>
    public decimal MainNoFlowOutputRatio { get; set; }

    /// <summary>有效主号状态(0=未计划 1=部分 2=满足)</summary>
    public int MainNoFlowStatus { get; set; }

    /// <summary>总批次数</summary>
    public int FlowTotalBatchCount { get; set; }

    /// <summary>未完成批数</summary>
    public int FlowIncompleteBatchCount { get; set; }

    /// <summary>最大剩余工量(天)</summary>
    public int FlowMaxRemainingWorkDays { get; set; }

    // ========== 手工字段（工单需求调整实体） ==========
    /// <summary>催单（手工填写）</summary>
    public bool IsUrging { get; set; }

    /// <summary>分批交货（手工填写）</summary>
    public bool IsBatchDelivery { get; set; }

    /// <summary>工单暂停（手工填写）</summary>
    public bool IsPaused { get; set; }

    /// <summary>调整备注（手工填写）</summary>
    public string? AdjustmentRemark { get; set; }

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
}
