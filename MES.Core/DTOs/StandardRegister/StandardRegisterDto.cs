namespace MES.Core.DTOs.ProductionStandard;

public class StandardRegisterDto
{
    public int Id { get; set; }
    public string StandardNo { get; set; } = string.Empty;
    public string StandardName { get; set; } = string.Empty;
    public string? RefSpecification { get; set; }
    public string? StandardLevel { get; set; }
    public string? ManufactureMethod { get; set; }
    public string? SteelType { get; set; }
    public string? Remark { get; set; }
}

public class StandardRegisterItemDto
{
    public int Id { get; set; }
    public int StandardRegisterId { get; set; }
    public int SeqNo { get; set; }
    public string? InspectionCategory { get; set; }
    public string InspectionItem { get; set; } = string.Empty;
    public string? IsMandatory { get; set; }
    public string? SamplingRequirement { get; set; }
    public string? ApplicableRange { get; set; }
    public string? RefStandard { get; set; }
    public string? DetailRequirement { get; set; }
}
