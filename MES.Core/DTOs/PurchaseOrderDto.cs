namespace MES.Core.DTOs;

public class PurchaseOrderDto
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = null!;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = null!;
    public string? ManualStatus { get; set; }
    public string MaterialCategory { get; set; } = null!;
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
    public string? SourceWorkOrderNo { get; set; }
    public string? Remark { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
}

public class CreatePurchaseOrderRequest
{
    public int SupplierId { get; set; }
    public DateTime OrderDate { get; set; }
    public string MaterialCategory { get; set; } = null!;
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public decimal? UnitWeight { get; set; }
    public int? Quantity { get; set; }
    public decimal Weight { get; set; }
    public DateTime RequiredDate { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? SourceWorkOrderNo { get; set; }
    public string? Remark { get; set; }
}

public class UpdatePurchaseOrderRequest
{
    public int SupplierId { get; set; }
    public string MaterialCategory { get; set; } = null!;
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public decimal? UnitWeight { get; set; }
    public int? Quantity { get; set; }
    public decimal Weight { get; set; }
    public DateTime RequiredDate { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? SourceWorkOrderNo { get; set; }
    public string? Remark { get; set; }
}

public class UpdateOrderStatusRequest
{
    public string? ManualStatus { get; set; }
}
