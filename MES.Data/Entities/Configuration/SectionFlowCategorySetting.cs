namespace MES.Data.Entities.Configuration;

/// <summary>
/// 段落分类设置 — 每个段落类别一行，存储用户可编辑的参数
/// </summary>
public class SectionFlowCategorySetting : BaseEntity
{
    public string CategoryCode { get; set; } = null!;

    public string CategoryName { get; set; } = null!;

    /// <summary>变异量预算日产（用户编辑）</summary>
    public decimal? DailyProductionTarget { get; set; }

    /// <summary>偏少天数值（用户编辑）</summary>
    public decimal? LowerLimitDays { get; set; }

    /// <summary>过多天数值（用户编辑）</summary>
    public decimal? UpperLimitDays { get; set; }

    public string? Remark { get; set; }

    public ICollection<SectionFlowCategoryItem> Items { get; set; } = new List<SectionFlowCategoryItem>();
}
