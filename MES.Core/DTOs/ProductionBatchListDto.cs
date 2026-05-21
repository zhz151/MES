namespace MES.Core.DTOs;

/// <summary>
/// 生产批次列表DTO
/// </summary>
public class ProductionBatchListDto
{
    public int Id { get; set; }
    public string BatchNo { get; set; } = null!;
    public string? TagNo { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
    public string WorkOrderNo { get; set; } = null!;
    public string SalesOrderNo { get; set; } = null!;
    public string ProductionMainNo { get; set; } = null!;
    public string? ProductionSubNo { get; set; }
    public string? ProductionType { get; set; }
    public string ManufacturingItem { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int ProductionRatio { get; set; }
    public DateTime? CurrentExecDate { get; set; }
    public string? CurrentGroupName { get; set; }
    public string? CurrentSectionName { get; set; }
    public string? CurrentEquipmentName { get; set; }
    public string? CurrentOutsource { get; set; }
    public string? CurrentSpec { get; set; }
    public string? NextSectionName { get; set; }
    public string? CorrespondingSpec { get; set; }
    public int? CurrentValidQty { get; set; }
    public decimal? CurrentValidWeight { get; set; }
    public string CreatedBy { get; set; } = null!;

    // ========== 工单冗余字段 ==========
    public DateTime SignDate { get; set; }
    public string Salesman { get; set; } = null!;
    public string? EndCustomer { get; set; }
    public DateTime DeliveryDate { get; set; }
    public bool DelayPenalty { get; set; }
    public string MaterialName { get; set; } = null!;
    public string SettlementMethod { get; set; } = null!;
    public string StandardCode { get; set; } = null!;
    public string DeliveryState { get; set; } = null!;
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public string LengthStatus { get; set; } = null!;
    public int TotalQuantity { get; set; }
    public decimal TotalMeters { get; set; }
    public decimal TotalWeight { get; set; }
    public string TechnicalRequirements { get; set; } = null!;

    /// <summary>
    /// 有效投料疑问：基于最近过程检验计算，正常/疑问
    /// </summary>
    public string? ValidInputQuestion { get; set; }

    // ========== 扩展字段（从 Entity 补充） ==========
    public string? Remark { get; set; }
    public string? SourceHeatNo { get; set; }
    public int TotalItemCount { get; set; }
    public string? SourceSpecification { get; set; }
    public int? InputQuantity { get; set; }
    public decimal? InputWeight { get; set; }
    public string? SolutionParams { get; set; }
    public string? QualityRemark { get; set; }
    public string? SourceMaterialType { get; set; }
    public string? SourceName { get; set; }
    public DateTime? InboundDate { get; set; }
}
