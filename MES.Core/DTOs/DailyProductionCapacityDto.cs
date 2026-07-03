namespace MES.Core.DTOs;

/// <summary>
/// 重点工序日产能力
/// </summary>
public class DailyProductionCapacityDto
{
    public int Id { get; set; }
    public string ProcessName { get; set; } = null!;
    public decimal DailyCapacity { get; set; }
    public string? Remark { get; set; }
}
