using MES.Core.Enums;

namespace MES.Core.DTOs.Warehouse;

/// <summary>
/// 更新入库批次请求
/// </summary>
public class UpdateInventoryBatchRequest
{
    public string? BatchNo { get; set; }
    public MaterialType? MaterialType { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public InboundSource? InboundSource { get; set; }
    public string? SourceName { get; set; }
    public DateTime? InboundDate { get; set; }
    public string? HeatNo { get; set; }
    public string? ProductionBatchNo { get; set; }
    public string? LengthStatus { get; set; }
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public int? InitialQuantity { get; set; }
    public decimal? InitialWeight { get; set; }
    public decimal? UnitWeight { get; set; }
    public decimal? Meters { get; set; }
    public string? ActualSpecification { get; set; }
    public DeliveryState? ManufacturingStatus { get; set; }
    public string? LocationArea { get; set; }
    public string? LocationRack { get; set; }
    public string? Remark { get; set; }
    public string? DefectReason { get; set; }
    public string? LiabilityType { get; set; }
    public string? OriginalSupplier { get; set; }
    public string? TagNo { get; set; }
    public string? DefectRemark { get; set; }
    public bool? IsLinkedToWorkOrder { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? SalesOrderNo { get; set; }
    public string? OrderItemIds { get; set; }
    public string? SourceOrderNo { get; set; }
    public int? SourceOrderSequence { get; set; }
}
