namespace MES.Core.DTOs;

/// <summary>
/// 销售催单列表 DTO（G1+G12 + 手工字段）
/// </summary>
public class SalesUrgingDto
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

    // ========== G12: 实时关注 ==========
    public int ScheduleStage { get; set; }
    public int? TotalRemainingWorkDays { get; set; }
    public string? UrgencyLevel { get; set; }
    public DateTime? EstimatedProcessCompletionDate { get; set; }
    public int? DaysDiffFromDelivery { get; set; }
    public string? RawMaterialLockRemark { get; set; }

    // ========== 手工字段（销售催单实体） ==========
    /// <summary>销售催单（手工填写）</summary>
    public bool IsSalesUrging { get; set; }

    /// <summary>催单备注（手工填写）</summary>
    public string? UrgingRemark { get; set; }

    // ========== 原料锁定字段（手工填写，存 SalesUrging 表） ==========
    /// <summary>预计到料日期</summary>
    public DateTime? EstimatedArrivalDate { get; set; }

    /// <summary>主号原锁齐全</summary>
    public bool IsMainNoMaterialComplete { get; set; }

    /// <summary>确认锁定</summary>
    public bool IsLockConfirmed { get; set; }

    // ========== 显示文本 ==========
    public string DelayPenaltyText => DelayPenalty ? "是" : "否";
    public string ScheduleStageText => ScheduleStage switch
    {
        0 => "无需排产", 1 => "原料锁定", 2 => "生产执行", 3 => "成品检验", _ => "未知"
    };
}
