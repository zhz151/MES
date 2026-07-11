namespace MES.Core.DTOs.Materials;

public class SupplierProfileDto
{
    public int Id { get; set; }
    public string SupplierCode { get; set; } = null!;
    public string SupplierName { get; set; } = null!;
    public string? MaterialCategory { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public string? Remark { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
}

public class CreateSupplierRequest
{
    public string SupplierName { get; set; } = null!;
    public string? MaterialCategory { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Remark { get; set; }
}

public class UpdateSupplierRequest
{
    public string? SupplierName { get; set; }
    public string? MaterialCategory { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public bool? IsActive { get; set; }
    public string? Remark { get; set; }
}
