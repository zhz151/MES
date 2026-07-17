using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Materials;

public class PurchaseOrderDto
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = null!;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public PurchaseOrderStatus Status { get; set; }
    public string StatusDisplay => EnumHelper.GetDisplayName(Status);
    public bool IsForceCompleted { get; set; }
    public MaterialType MaterialCategory { get; set; }
    public string MaterialCategoryDisplay => EnumHelper.GetDisplayName(MaterialCategory);
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public decimal? UnitWeight { get; set; }
    public int? Quantity { get; set; }
    public decimal Weight { get; set; }
    public DateTime RequiredDate { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalAmount { get; set; }
    public DateTime? LastArrivalDate { get; set; }
    public int ReceivedQuantity { get; set; }
    public decimal ReceivedWeight { get; set; }
    public int? InputMultiple { get; set; }
    public string? SourceWorkOrderNo { get; set; }
    public string? Remark { get; set; }
    public DateTimeOffset CreatedTime { get; set; }

    // ========== 工单来源字段（从 WorkOrder 关联查询） ==========
    public string? WoSalesOrderNo { get; set; }
    public string? WoProductionMainNo { get; set; }
    public string? WoProductionSubNo { get; set; }
    public DateTime? WoSignDate { get; set; }
    public string? WoSalesman { get; set; }
    public string? WoEndCustomer { get; set; }
    public DateTime? WoDeliveryDate { get; set; }
    public bool WoDelayPenalty { get; set; }
    public SettlementMethod? WoSettlementMethod { get; set; }
    public string? WoSettlementMethodDisplay => WoSettlementMethod.HasValue ? EnumHelper.GetDisplayName(WoSettlementMethod.Value) : null;
    public string? WoPlantGrade { get; set; }
    public string? WoSpecification { get; set; }
    public LengthStatus? WoLengthStatus { get; set; }
    public string? WoLengthStatusDisplay => WoLengthStatus.HasValue ? EnumHelper.GetDisplayName(WoLengthStatus.Value) : null;
    public decimal? WoMaxLength { get; set; }
    public int? WoTotalQuantity { get; set; }
    public decimal? WoTotalWeight { get; set; }
    public DeliveryState? WoDeliveryState { get; set; }
    public string? WoDeliveryStateDisplay => WoDeliveryState.HasValue ? EnumHelper.GetDisplayName(WoDeliveryState.Value) : null;
    public int? WoTotalItemCount { get; set; }
}

public class CreatePurchaseOrderRequest
{
    public int SupplierId { get; set; }
    public DateTime OrderDate { get; set; }
    public MaterialType MaterialCategory { get; set; }
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public decimal? UnitWeight { get; set; }
    public int? Quantity { get; set; }
    public decimal Weight { get; set; }
    public DateTime RequiredDate { get; set; }
    public decimal? UnitPrice { get; set; }
    public int? InputMultiple { get; set; }
    public string? SourceWorkOrderNo { get; set; }
    public string? Remark { get; set; }
}

public class UpdatePurchaseOrderRequest
{
    public int SupplierId { get; set; }
    public MaterialType MaterialCategory { get; set; }
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public decimal? UnitWeight { get; set; }
    public int? Quantity { get; set; }
    public decimal Weight { get; set; }
    public DateTime RequiredDate { get; set; }
    public decimal? UnitPrice { get; set; }
    public int? InputMultiple { get; set; }
    public string? SourceWorkOrderNo { get; set; }
    public string? Remark { get; set; }
}

public class UpdateOrderStatusRequest
{
    public bool IsForceCompleted { get; set; }
}
