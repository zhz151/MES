namespace MES.Core.DTOs;

public class StandardGradeMappingDto
{
    public int Id { get; set; }

    public string StandardGrade { get; set; } = string.Empty;
    public string PlantGrade { get; set; } = string.Empty;
    public decimal Density { get; set; }

    public string? HeatTreatment { get; set; }

    public bool SpecialMaterial { get; set; }

    public string? SpecialNote { get; set; }
    public string? Remark { get; set; }
}