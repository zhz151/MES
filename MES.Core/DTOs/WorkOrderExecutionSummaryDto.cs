namespace MES.Core.DTOs;

/// <summary>
/// 工单执行状况 DTO（列表页展示）
/// </summary>
public class WorkOrderExecutionSummaryDto
{
    public int Id { get; set; }

    // ========== 工单标识 ==========
    public int WorkOrderId { get; set; }
    public string WorkOrderNo { get; set; } = null!;
    public DateTime? LastRefreshTime { get; set; }

    // ========== Group 1: 工单基础数据 ==========
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

    // ========== Group 2: 用料计划 ==========
    public DateTime? LatestPlanDate { get; set; }
    public decimal MaterialPlanRate { get; set; }
    public int MaterialPlanStatus { get; set; }
    public decimal MainNoMaterialPlanRate { get; set; }
    public int MainNoMaterialPlanStatus { get; set; }

    // ========== Group 3: 投料数据 ==========
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

    // ========== Group 4: 有效数据 ==========
    public int ValidBatchCount { get; set; }
    public int ValidInputQuantity { get; set; }
    public decimal ValidInputWeight { get; set; }
    public decimal ValidOutputQty { get; set; }
    public decimal ValidOutputWeight { get; set; }
    public decimal ValidInputOutputRatio { get; set; }
    public int ValidInputStatus { get; set; }
    public decimal MainNoValidInputOutputRatio { get; set; }
    public int MainNoValidInputStatus { get; set; }

    // ========== 显示用 ==========
    public string MaterialPlanStatusText => MaterialPlanStatus switch
    {
        0 => "未计划", 1 => "部分", 2 => "理论满足", 3 => "满足", 4 => "超量", _ => "未知"
    };

    public string MainNoMaterialPlanStatusText => MainNoMaterialPlanStatus switch
    {
        0 => "未计划", 1 => "部分", 2 => "理论满足", 3 => "满足", 4 => "超量", _ => "未知"
    };

    public string InputStatusText => InputStatus switch
    {
        0 => "未投料", 1 => "部分", 2 => "满足", _ => "未知"
    };

    public string MainNoInputStatusText => MainNoInputStatus switch
    {
        0 => "未投料", 1 => "部分", 2 => "满足", _ => "未知"
    };

    public string ValidInputStatusText => ValidInputStatus switch
    {
        0 => "未投料", 1 => "部分", 2 => "满足", _ => "未知"
    };

    public string MainNoValidInputStatusText => MainNoValidInputStatus switch
    {
        0 => "未计划", 1 => "部分", 2 => "满足", _ => "未知"
    };

    public string DelayPenaltyText => DelayPenalty ? "是" : "否";
}
