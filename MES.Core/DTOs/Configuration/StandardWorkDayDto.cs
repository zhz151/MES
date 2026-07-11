namespace MES.Core.DTOs.Configuration;

/// <summary>
/// 标准工量天数 DTO
/// </summary>
public class StandardWorkDayDto
{
    public int Id { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string? PlantGradePrefix { get; set; }
    public double StandardDays { get; set; }
    public string? Remark { get; set; }
}
