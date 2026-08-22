namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 冷轧产能配置 DTO
/// </summary>
public class ColdRollCapacityDto
{
    public int Id { get; set; }
    public string ProcessType { get; set; } = "";
    public string BilletSpec { get; set; } = "";
    public string RollingSpec { get; set; } = "";
    public bool IsFinished { get; set; }
    public string? MachineNo { get; set; }
    public decimal? DailyOutput { get; set; }
    public int SampleCount { get; set; }
    public DateTime? LastConfirmedAt { get; set; }
    public DateTime UpdatedTime { get; set; }
}
