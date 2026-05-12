using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

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

    [MaxLength(500)]
    public string? Remark { get; set; }

    // 仓库来源（允许更新）
    [MaxLength(50)]
    public string? SourceBatchNo { get; set; }

    public int? WarehouseId { get; set; }

    [MaxLength(30)]
    public string? SourceMaterialType { get; set; }

    [MaxLength(200)]
    public string? SourceName { get; set; }

    [MaxLength(50)]
    public string? SourceHeatNo { get; set; }

    [MaxLength(50)]
    public string? SourcePlantGrade { get; set; }

    [MaxLength(100)]
    public string? SourceSpecification { get; set; }

    [MaxLength(20)]
    public string? SourceLengthStatus { get; set; }

    public decimal? SourceUnitWeight { get; set; }

    public int? InputQuantity { get; set; }

    public decimal? InputWeight { get; set; }

    // 批次字段
    public bool? IsForceCompleted { get; set; }

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

    [MaxLength(20)]
    public string? MaterialName { get; set; }

    [MaxLength(20)]
    public string? SettlementMethod { get; set; }

    [MaxLength(50)]
    public string? StandardCode { get; set; }

    [MaxLength(50)]
    public string? DeliveryState { get; set; }

    [MaxLength(50)]
    public string? PlantGrade { get; set; }

    [MaxLength(100)]
    public string? Specification { get; set; }

    public decimal? OuterDiameterNegative { get; set; }
    public decimal? OuterDiameterPositive { get; set; }
    public decimal? WallThicknessNegative { get; set; }
    public decimal? WallThicknessPositive { get; set; }

    [MaxLength(20)]
    public string? LengthStatus { get; set; }

    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public int? TotalQuantity { get; set; }
    public decimal? TotalMeters { get; set; }
    public decimal? TotalWeight { get; set; }
    public int? TotalItemCount { get; set; }

    public string? ItemDetails { get; set; }

    [MaxLength(500)]
    public string? TechnicalRequirements { get; set; }

    [Required(ErrorMessage = "RowVersion不能为空")]
    public byte[] RowVersion { get; set; } = null!;
}
