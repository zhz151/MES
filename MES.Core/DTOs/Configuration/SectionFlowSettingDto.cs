namespace MES.Core.DTOs.Configuration;

/// <summary>
/// 生产段流转量设置更新 DTO
/// </summary>
public class SectionFlowSettingUpdateDto
{
    public int Id { get; set; }
    public decimal? DailyProductionTarget { get; set; }
    public decimal? LowerLimitDays { get; set; }
    public decimal? UpperLimitDays { get; set; }
}
