namespace MES.Data.Entities.Configuration;

/// <summary>
/// 段落日产配置 — 每个生产段落一行，存储用户可编辑的参数。
/// 段落包含的(工序组,工段,产类)组合由组合归类表 CombinationGroups 的「归属段落」承载。
/// </summary>
public class SectionParagraphConfig : BaseEntity
{
    /// <summary>段落类别（中文）</summary>
    public string ParagraphName { get; set; } = null!;

    /// <summary>展示序号（汇总表显示顺序）</summary>
    public int DisplayOrder { get; set; }

    /// <summary>日流转设定（吨/天，用户编辑）</summary>
    public decimal? DailyFlowTarget { get; set; }

    /// <summary>偏少天数值（用户编辑）</summary>
    public decimal? LowerLimitDays { get; set; }

    /// <summary>过多天数值（用户编辑）</summary>
    public decimal? UpperLimitDays { get; set; }

    public string? Remark { get; set; }
}
