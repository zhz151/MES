namespace MES.Core.DTOs;

/// <summary>
/// 段落分类设置 DTO — 用于参数表管理页面
/// </summary>
public class SectionFlowCategorySettingDto
{
    public int Id { get; set; }
    public string CategoryCode { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
    public decimal? DailyProductionTarget { get; set; }
    public decimal? LowerLimitDays { get; set; }
    public decimal? UpperLimitDays { get; set; }
    public string? Remark { get; set; }
    public List<SectionFlowCategoryItemDto> Items { get; set; } = new();
}
