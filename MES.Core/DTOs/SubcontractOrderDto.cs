namespace MES.Core.DTOs;

public class SubcontractOrderDto
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = null!;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = null!;
    public string? ManualStatus { get; set; }
    public string OutMaterialCategory { get; set; } = null!;
    public string OutPlantGrade { get; set; } = null!;
    public string OutSpecification { get; set; } = null!;
    public int OutQuantity { get; set; }
    public decimal OutWeight { get; set; }
    public DateTime? ReturnDeadline { get; set; }
    public int? InQuantity { get; set; }
    public decimal? InWeight { get; set; }
    public string? SourceWorkOrderNo { get; set; }
    public string? Remark { get; set; }
    public List<SubcontractReturnItemDto> ReturnItems { get; set; } = new();
    public DateTimeOffset CreatedTime { get; set; }
}

public class SubcontractReturnItemDto
{
    public int Id { get; set; }
    public int SubcontractOrderId { get; set; }
    public int Sequence { get; set; }
    public string ProcessType { get; set; } = null!;
    public string MaterialCategory { get; set; } = null!;
    public string ProcessSpecification { get; set; } = null!;
    public string? ProcessStatusRemark { get; set; }
    public decimal? ProcessUnitPrice { get; set; }
    public decimal? ProcessTotalAmount { get; set; }
    public string? SourceWorkOrderNo { get; set; }
}

public class CreateSubcontractOrderRequest
{
    public int SupplierId { get; set; }
    public DateTime OrderDate { get; set; }
    public string OutMaterialCategory { get; set; } = null!;
    public string OutPlantGrade { get; set; } = null!;
    public string OutSpecification { get; set; } = null!;
    public int OutQuantity { get; set; }
    public decimal OutWeight { get; set; }
    public DateTime? ReturnDeadline { get; set; }
    public string? SourceWorkOrderNo { get; set; }
    public string? Remark { get; set; }
    public List<CreateReturnItemRequest> ReturnItems { get; set; } = new();
}

public class CreateReturnItemRequest
{
    public string ProcessType { get; set; } = null!;
    public string MaterialCategory { get; set; } = null!;
    public string ProcessSpecification { get; set; } = null!;
    public string? ProcessStatusRemark { get; set; }
    public decimal? ProcessUnitPrice { get; set; }
    public decimal? ProcessTotalAmount { get; set; }
    public string? SourceWorkOrderNo { get; set; }
}

public class UpdateSubcontractOrderRequest
{
    public int SupplierId { get; set; }
    public string OutMaterialCategory { get; set; } = null!;
    public string OutPlantGrade { get; set; } = null!;
    public string OutSpecification { get; set; } = null!;
    public int OutQuantity { get; set; }
    public decimal OutWeight { get; set; }
    public DateTime? ReturnDeadline { get; set; }
    public string? SourceWorkOrderNo { get; set; }
    public string? Remark { get; set; }
    public List<CreateReturnItemRequest> ReturnItems { get; set; } = new();
}
