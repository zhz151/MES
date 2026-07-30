using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Batch;

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
    public MaterialType ManufacturingItem { get; set; }
    public string ManufacturingItemDisplay => EnumHelper.GetDisplayName(ManufacturingItem);
    public BatchStatus Status { get; set; }
    public string StatusDisplay => EnumHelper.GetDisplayName(Status);
    public int ProductionRatio { get; set; }
    public DateTime? CurrentExecDate { get; set; }
    public string? CurrentGroupName { get; set; }
    public string? CurrentSectionName { get; set; }
    public string? CurrentEquipmentName { get; set; }
    public string? CurrentOutsource { get; set; }
    public string? CurrentSpec { get; set; }
    public string? NextSectionName { get; set; }
    public string? CorrespondingSpec { get; set; }
    public string? NextProcess { get; set; }
    public bool? CurrentSectionCompleted { get; set; }
    public int RemainingWorkDays { get; set; }
    public int TotalWorkDays { get; set; }
    public int? CurrentValidQty { get; set; }
    public int? CurrentValidWeight { get; set; }
    public string CreatedBy { get; set; } = null!;

    // ========== 工单冗余字段 ==========
    public DateTime SignDate { get; set; }
    public string Salesman { get; set; } = null!;
    public string? EndCustomer { get; set; }
    public DateTime DeliveryDate { get; set; }
    public bool DelayPenalty { get; set; }
    public string MaterialName { get; set; } = null!;
    public SettlementMethod SettlementMethod { get; set; }
    public string SettlementMethodDisplay => EnumHelper.GetDisplayName(SettlementMethod);
    public string StandardCode { get; set; } = null!;
    public DeliveryState DeliveryState { get; set; }
    public string DeliveryStateDisplay => EnumHelper.GetDisplayName(DeliveryState);
    public DeliveryState? ManufacturingStatus { get; set; }
    public string? ManufacturingStatusDisplay => ManufacturingStatus.HasValue ? EnumHelper.GetDisplayName(ManufacturingStatus.Value) : null;
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public LengthStatus LengthStatus { get; set; }
    public string LengthStatusDisplay => EnumHelper.GetDisplayName(LengthStatus);
    public int TotalQuantity { get; set; }
    public decimal TotalMeters { get; set; }
    public decimal TotalWeight { get; set; }
    public string TechnicalRequirements { get; set; } = null!;

    /// <summary>
    /// 有效投料变更：有效投料支数与领料支数是否一致，有/无
    /// </summary>
    public bool? HasInputChange { get; set; }

    // ========== 扩展字段（从 Entity 补充） ==========
    public string? Remark { get; set; }
    public string? SourceHeatNo { get; set; }
    public int TotalItemCount { get; set; }
    public string? SourceSpecification { get; set; }
    public int? InputQuantity { get; set; }
    public decimal? InputWeight { get; set; }
    public string? SolutionParams { get; set; }
    public string? QualityRemark { get; set; }
    public MaterialType? SourceMaterialType { get; set; }
    public string? SourceMaterialTypeDisplay => SourceMaterialType.HasValue ? EnumHelper.GetDisplayName(SourceMaterialType.Value) : null;
    public string? SourceName { get; set; }
}
