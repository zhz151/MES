namespace MES.Core.DTOs;


public class ProductionStandardDto
{

    public int Id { get; set; }


    public string StandardCode { get; set; } = string.Empty;

    public string StandardName { get; set; } = string.Empty;


    public string? Remark { get; set; }


    public int SortOrder { get; set; }


    public bool IsActive { get; set; }
}