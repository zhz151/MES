namespace MES.Data.Entities;

/// <summary>
/// 工单执行状况汇总表（物化读模型）
/// 通过 [即时更新] 按钮手动刷新，不提供手工增删改
/// </summary>
public class WorkOrderExecutionSummary : BaseEntity
{
    // ========== 工单标识 ==========
    /// <summary>工单ID（唯一，一个工单一条记录）</summary>
    public int WorkOrderId { get; set; }

    /// <summary>工单号</summary>
    public string WorkOrderNo { get; set; } = null!;

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
    /// <summary>用料计划截止日期</summary>
    public DateTime? LatestPlanDate { get; set; }

    /// <summary>工单满足率(%)</summary>
    public decimal MaterialPlanRate { get; set; }

    /// <summary>工单用料计划状态(0=未计划 1=部分 2=理论满足 3=满足 4=超量)</summary>
    public int MaterialPlanStatus { get; set; }

    /// <summary>主号满足率(%)</summary>
    public decimal MainNoMaterialPlanRate { get; set; }

    /// <summary>关联主号用料状态</summary>
    public int MainNoMaterialPlanStatus { get; set; }

    // ========== Group 3: 投料数据（所有关联批次） ==========
    /// <summary>投料起始日</summary>
    public DateTime? InputStartDate { get; set; }

    /// <summary>投料截止日</summary>
    public DateTime? InputEndDate { get; set; }

    /// <summary>投料总批次数</summary>
    public int TotalBatchCount { get; set; }

    /// <summary>投料总支数（SUM InputQuantity）</summary>
    public int InputQuantity { get; set; }

    /// <summary>投料总重量（SUM InputWeight）</summary>
    public decimal InputWeight { get; set; }

    /// <summary>理论生产成品支数</summary>
    public decimal TheoreticalOutputQty { get; set; }

    /// <summary>理论生产成品重量</summary>
    public decimal TheoreticalOutputWeight { get; set; }

    /// <summary>工单投料成品比(%)</summary>
    public decimal InputOutputRatio { get; set; }

    /// <summary>工单投料状态(0=未投料 1=部分 2=满足)</summary>
    public int InputStatus { get; set; }

    /// <summary>关联主号投料成品比(%)</summary>
    public decimal MainNoInputOutputRatio { get; set; }

    /// <summary>关联主号投料状态</summary>
    public int MainNoInputStatus { get; set; }

    // ========== Group 4: 有效数据（排除作废批次） ==========
    /// <summary>有效在产批次数（排除 Cancelled）</summary>
    public int ValidBatchCount { get; set; }

    /// <summary>有效投料总支数（SUM CurrentValidQty）</summary>
    public int ValidInputQuantity { get; set; }

    /// <summary>有效投料总重量（SUM CurrentValidWeight）</summary>
    public decimal ValidInputWeight { get; set; }

    /// <summary>有效生产成品支数</summary>
    public decimal ValidOutputQty { get; set; }

    /// <summary>有效生产成品重量</summary>
    public decimal ValidOutputWeight { get; set; }

    /// <summary>有效工单投料成品比(%)</summary>
    public decimal ValidInputOutputRatio { get; set; }

    /// <summary>有效工单投料状态</summary>
    public int ValidInputStatus { get; set; }

    /// <summary>现有效关联主号投料成品比(%)</summary>
    public decimal MainNoValidInputOutputRatio { get; set; }

    /// <summary>现有效关联主号投料状态(0=未计划 1=部分 2=满足)</summary>
    public int MainNoValidInputStatus { get; set; }

    // ========== 刷新追踪 ==========
    /// <summary>最后刷新时间</summary>
    public DateTime? LastRefreshTime { get; set; }
}
