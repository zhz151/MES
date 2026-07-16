using MES.Core.Enums;

namespace MES.Core.DTOs.Batch;

/// <summary>
/// 生产批次详情DTO
/// </summary>
public class ProductionBatchDetailDto
{
    // ========== 批次自身 ==========
    public int Id { get; set; }
    public string BatchNo { get; set; } = null!;
    public BatchStatus Status { get; set; }
    public string? TagNo { get; set; }
    public string? ProductionType { get; set; }
    public ManufacturingItem ManufacturingItem { get; set; }
    public int ProductionRatio { get; set; }
    public bool IsForceCompleted { get; set; }
    public string? QualityRemark { get; set; }
    public string? SolutionParams { get; set; }
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
    public string? Remark { get; set; }

    // ========== 工单冗余 ==========
    public string WorkOrderNo { get; set; } = null!;
    public string SalesOrderNo { get; set; } = null!;
    public string ProductionMainNo { get; set; } = null!;
    public string? ProductionSubNo { get; set; }
    public string OrderItemIds { get; set; } = null!;
    public DateTime SignDate { get; set; }
    public string Salesman { get; set; } = null!;
    public string? EndCustomer { get; set; }
    public DateTime DeliveryDate { get; set; }
    public bool DelayPenalty { get; set; }
    public string MaterialName { get; set; } = null!;
    public SettlementMethod SettlementMethod { get; set; }
    public string StandardCode { get; set; } = null!;
    public DeliveryState DeliveryState { get; set; }
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public decimal OuterDiameterNegative { get; set; }
    public decimal OuterDiameterPositive { get; set; }
    public decimal WallThicknessNegative { get; set; }
    public decimal WallThicknessPositive { get; set; }
    public LengthStatus LengthStatus { get; set; }
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalMeters { get; set; }
    public decimal TotalWeight { get; set; }
    public int TotalItemCount { get; set; }
    public string? ItemDetails { get; set; }
    public string TechnicalRequirements { get; set; } = null!;

    // ========== 仓库冗余 ==========
    public string? SourceBatchNo { get; set; }
    public int? WarehouseId { get; set; }
    public string? SourceMaterialType { get; set; }
    public string? InboundSource { get; set; }
    public string? SourceName { get; set; }
    public DateTime? InboundDate { get; set; }
    public string? SourceHeatNo { get; set; }
    public string? SourcePlantGrade { get; set; }
    public string? SourceSpecification { get; set; }
    public string? SourceLengthStatus { get; set; }
    public decimal? SourceUnitWeight { get; set; }
    public int? InputQuantity { get; set; }
    public decimal? InputWeight { get; set; }
    public int? CurrentValidQty { get; set; }
    public decimal? CurrentValidWeight { get; set; }

    // ========== 审计字段 ==========
    public DateTimeOffset CreatedTime { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTimeOffset UpdatedTime { get; set; }
    public string UpdatedBy { get; set; } = null!;

    // ========== 乐观锁 ==========
    public byte[] RowVersion { get; set; } = null!;

    // ========== 工序组列表 ==========
    public List<ProcessGroupDto> ProcessGroups { get; set; } = new();
}
