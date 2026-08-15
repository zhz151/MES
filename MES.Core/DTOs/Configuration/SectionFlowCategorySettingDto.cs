namespace MES.Core.DTOs.Configuration;

/// <summary>
/// 段落分类设置 DTO — 用于参数表管理页面。类别包含的(工序组,工段,产类)组合由组合归类表承载。
/// </summary>
public class SectionFlowCategorySettingDto
{
    public int Id { get; set; }
    public string CategoryName { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public decimal? DailyProductionTarget { get; set; }
    public decimal? LowerLimitDays { get; set; }
    public decimal? UpperLimitDays { get; set; }
    public string? Remark { get; set; }
}
