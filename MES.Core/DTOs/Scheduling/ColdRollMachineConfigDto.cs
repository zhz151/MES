namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 冷轧机台数配置 DTO
/// </summary>
public class ColdRollMachineConfigDto
{
    public int Id { get; set; }
    public string ProcessType { get; set; } = "";
    public int OwnedCount { get; set; }
    public int MinMachines { get; set; }
    public int MaxMachines { get; set; }
    public decimal? EstimatedDailyOutput { get; set; }
    public string? Remark { get; set; }
    public DateTime UpdatedTime { get; set; }
}
