namespace MES.Core.DTOs;

/// <summary>
/// 可用库存批次（已出库生产领用且尚未被生产批次引用）
/// </summary>
public class AvailableBatchDto
{
    public string BatchNo { get; set; } = null!;
    public int WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public string? MaterialType { get; set; }
    public string? InboundSource { get; set; }
    public string? SourceName { get; set; }
    public DateTime? InboundDate { get; set; }
    public string? HeatNo { get; set; }
    public int? OutboundQuantity { get; set; }
    public decimal? OutboundWeight { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public string? LengthStatus { get; set; }
    public decimal? UnitWeight { get; set; }
}
