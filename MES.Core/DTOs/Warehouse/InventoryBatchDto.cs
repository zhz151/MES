using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Warehouse;

/// <summary>
/// 库存批次 DTO
/// </summary>
public class InventoryBatchDto
{
    public int Id { get; set; }
    public string BatchNo { get; set; } = string.Empty;

    // 仓库
    public int WarehouseId { get; set; }

    // 物料
    public MaterialType MaterialType { get; set; }
    public string MaterialTypeDisplay => EnumHelper.GetDisplayName(MaterialType);
    public string PlantGrade { get; set; } = string.Empty;
    public string Specification { get; set; } = string.Empty;

    // 来源
    public InboundSource InboundSource { get; set; }
    public string InboundSourceDisplay => EnumHelper.GetDisplayName(InboundSource);
    public string SourceName { get; set; } = string.Empty;
    public DateTime InboundDate { get; set; }

    // 炉号/批号/长度
    public string? HeatNo { get; set; }
    public string? ProductionBatchNo { get; set; }
    public string? LengthStatus { get; set; }
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }

    // 数量重量
    public int InitialQuantity { get; set; }
    public decimal InitialWeight { get; set; }
    public decimal? UnitWeight { get; set; }
    public decimal? Meters { get; set; }
    public decimal? RemainingMeters { get; set; }
    public int RemainingQuantity { get; set; }
    public decimal RemainingWeight { get; set; }

    // 实际规格
    public string? ActualSpecification { get; set; }

    // 位置状态
    public DeliveryState? SurfaceCondition { get; set; }
    public string? SurfaceConditionDisplay => SurfaceCondition.HasValue ? EnumHelper.GetDisplayName(SurfaceCondition.Value) : null;
    public string? LocationArea { get; set; }
    public string? LocationRack { get; set; }
    public string? Remark { get; set; }

    // 次品
    public string? DefectReason { get; set; }
    public string? LiabilityType { get; set; }
    public string? OriginalSupplier { get; set; }
    public string? TagNo { get; set; }
    public string? DefectRemark { get; set; }

    // 工单关联
    public bool IsLinkedToWorkOrder { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? SalesOrderNo { get; set; }
    public string? OrderItemIds { get; set; }

    // 跨上下文关联
    public string? SourceOrderNo { get; set; }
    public int? SourceOrderSequence { get; set; }
}
