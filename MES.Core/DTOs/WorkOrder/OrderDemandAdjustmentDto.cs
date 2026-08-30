using MES.Core.Enums;
using MES.Core.Helpers;

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
    public string? EndCustomer { get; set; }
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

    // ========== 手工字段（工单需求调整实体） ==========
    /// <summary>催单（手工填写）</summary>
    public bool IsUrging { get; set; }

    /// <summary>分批交货（手工填写）</summary>
    public bool IsBatchDelivery { get; set; }

    /// <summary>工单暂停（手工填写）</summary>
    public bool IsPaused { get; set; }

    /// <summary>强制完成（手工填写，主号级联动，与暂停互斥；置是后主号-关注=主号完成）</summary>
    public bool IsForceCompleted { get; set; }

    /// <summary>调整备注（手工填写）</summary>
    public string? AdjustmentRemark { get; set; }

    // ========== 审计字段（取自源头 WorkOrder） ==========
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }

    // ========== 显示文本 ==========
    public string DelayPenaltyText => DelayPenalty ? "是" : "否";
    public string ScheduleStageText => IntStatusDisplayHelper.GetScheduleStageText(ScheduleStage);
}
