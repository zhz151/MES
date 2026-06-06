namespace MES.Core.DTOs;

/// <summary>
/// 日产估算 DTO
/// </summary>
public class DailyOutputEstimateDto
{
    public int Id { get; set; }
    public decimal MinOuterDiameter { get; set; }
    public decimal DailyOutputTons { get; set; }
    public string? Remark { get; set; }
}
