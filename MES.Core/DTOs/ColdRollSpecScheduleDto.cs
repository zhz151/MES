namespace MES.Core.DTOs;

/// <summary>
/// 冷轧排程 DTO
/// </summary>
public class ColdRollSpecScheduleDto
{
    public int Id { get; set; }
    public string ProcessType { get; set; } = "";
    public string BilletSpec { get; set; } = "";
    public string RollingSpec { get; set; } = "";
    public bool IsFinished { get; set; }
    public string? MachineNo { get; set; }
    public string CompletionType { get; set; } = "None";
    public string RollType { get; set; } = "None";
    public string? MergeDisplay { get; set; }
    public string? Remark { get; set; }
    public DateTime UpdatedTime { get; set; }
}
