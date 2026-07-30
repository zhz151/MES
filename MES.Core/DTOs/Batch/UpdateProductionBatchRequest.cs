using System.ComponentModel.DataAnnotations;
using MES.Core.Enums;

namespace MES.Core.DTOs.Batch;

/// <summary>
/// 更新生产批次请求
/// </summary>
public class UpdateProductionBatchRequest
{
    [MaxLength(50)]
    public string? TagNo { get; set; }

    [MaxLength(500)]
    public string? QualityRemark { get; set; }

    [MaxLength(500)]
    public string? SolutionParams { get; set; }

    public MaterialType? ManufacturingItem { get; set; }

    public ProductionType? ProductionType { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    // 仓库来源（允许更新）
    [MaxLength(50)]
    public string? SourceBatchNo { get; set; }

    public MaterialType? SourceMaterialType { get; set; }

    [MaxLength(200)]
    public string? SourceName { get; set; }

    [MaxLength(50)]
    public string? SourceHeatNo { get; set; }

    [MaxLength(50)]
    public string? SourcePlantGrade { get; set; }

    [MaxLength(100)]
    public string? SourceSpecification { get; set; }

    public MES.Core.Enums.LengthStatus? SourceLengthStatus { get; set; }

    public decimal? SourceUnitWeight { get; set; }

    public int? InputQuantity { get; set; }

    public decimal? InputWeight { get; set; }

    public BatchInputType? InputType { get; set; }

    [MaxLength(500)]
    public string? SourceRemark { get; set; }

    [MaxLength(50)]
    public string? SourceProductionNo { get; set; }

    public int? CurrentValidQty { get; set; }

    public int? CurrentValidWeight { get; set; }

    // 批次字段
    public bool? IsForceCompleted { get; set; }

    public int? ProductionRatio { get; set; }

    // ========== 工单冗余字段（全部可空，有值时更新） ==========

    [MaxLength(50)]
    public string? WorkOrderNo { get; set; }

    [MaxLength(50)]
    public string? SalesOrderNo { get; set; }

    [MaxLength(50)]
    public string? ProductionMainNo { get; set; }

    [MaxLength(50)]
    public string? ProductionSubNo { get; set; }

    [MaxLength(500)]
    public string? OrderItemIds { get; set; }

    public DateTime? SignDate { get; set; }

    [MaxLength(50)]
    public string? Salesman { get; set; }

    [MaxLength(200)]
    public string? EndCustomer { get; set; }

    public DateTime? DeliveryDate { get; set; }

    public bool? DelayPenalty { get; set; }

    public PipeManufacturingType? MaterialName { get; set; }

    public SettlementMethod? SettlementMethod { get; set; }

    [MaxLength(50)]
    public string? StandardCode { get; set; }

    public DeliveryState? DeliveryState { get; set; }

    public DeliveryState? ManufacturingStatus { get; set; }

    [MaxLength(50)]
    public string? PlantGrade { get; set; }

    [MaxLength(100)]
    public string? Specification { get; set; }

    public decimal? OuterDiameterNegative { get; set; }
    public decimal? OuterDiameterPositive { get; set; }
    public decimal? WallThicknessNegative { get; set; }
    public decimal? WallThicknessPositive { get; set; }

    public LengthStatus? LengthStatus { get; set; }

    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public int? TotalQuantity { get; set; }
    public decimal? TotalMeters { get; set; }
    public decimal? TotalWeight { get; set; }
    public int? TotalItemCount { get; set; }

    public string? ItemDetails { get; set; }

    public RequirementType? TechnicalRequirements { get; set; }

    [Required(ErrorMessage = "RowVersion不能为空")]
    public byte[] RowVersion { get; set; } = null!;

    // ========== 合并投料 ==========

    /// <summary>
    /// 来源批次列表（支持多库存批次合并投料）
    /// </summary>
    public List<SourceBatchItemRequest>? SourceItems { get; set; }

}
