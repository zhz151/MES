namespace MES.Data.Entities.Configuration;

/// <summary>
/// 段落日产配置 — 每个生产段落一行，存储用户可编辑的参数。
/// 段落由 3 类配置自动生成（冷轧拔=机台组显示名 / 普通工段=StandardWorkDays / 检验=固定），随配置增减，仅参数可编辑。
/// </summary>
public class SectionParagraphConfig : BaseEntity
{
    /// <summary>段落显示名（冷轧=机台组 DisplayName、普通=工段中文名、检验=固定中文）</summary>
    public string ParagraphName { get; set; } = null!;

    /// <summary>稳定 Key（冷轧=机台组 GroupKey、普通=SectionKey、检验=固定中文）；存量旧段落未映射时可为空，首次同步补齐/清理</summary>
    public string? ParagraphKey { get; set; }

    /// <summary>段落类别类型（Cold/Section/Fixed，见 ParagraphCategoryTypes）；存量旧段落未映射时可为空，首次同步补齐/清理</summary>
    public string? CategoryType { get; set; }

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
