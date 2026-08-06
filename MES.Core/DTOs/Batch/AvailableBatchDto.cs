using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Batch;

/// <summary>
/// 可用库存批次（已出库生产领用且尚未被生产批次引用）
/// </summary>
public class AvailableBatchDto
{
    public int Id { get; set; }

    /// <summary>
    /// 出库记录ID（用于精确排除已被引用的出库记录）
    /// </summary>
    public long OutboundRecordId { get; set; }
    public string BatchNo { get; set; } = null!;
    public int WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public MaterialType? MaterialType { get; set; }
    public string? MaterialTypeDisplay => MaterialType.HasValue ? EnumHelper.GetDisplayName(MaterialType.Value) : null;
    public InboundSource? InboundSource { get; set; }
    public string? InboundSourceDisplay => InboundSource.HasValue ? EnumHelper.GetDisplayName(InboundSource.Value) : null;
    public string? SourceName { get; set; }
    public DateTime? InboundDate { get; set; }
    public string? HeatNo { get; set; }
    public int? OutboundQuantity { get; set; }
    public decimal? OutboundWeight { get; set; }
    public DateTime? OutboundDate { get; set; }
    public string? OutboundRemark { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public LengthStatus? LengthStatus { get; set; }
    public decimal? UnitWeight { get; set; }
}
