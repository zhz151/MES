namespace MES.Core.DTOs;

public class UpdateProductionStandardRequest
{
    public string? StandardCode { get; set; }

    public string? StandardName { get; set; }

    public string? Remark { get; set; }

    public int? SortOrder { get; set; }

    public bool? IsActive { get; set; }
}