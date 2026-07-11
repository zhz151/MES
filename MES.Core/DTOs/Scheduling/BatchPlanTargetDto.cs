namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 批次计划产量目标 DTO
/// </summary>
public class BatchPlanTargetDto
{
    public int Id { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public decimal DailyTarget { get; set; }
}
