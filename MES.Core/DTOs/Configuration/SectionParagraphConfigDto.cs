namespace MES.Core.DTOs.Configuration;

/// <summary>
/// 段落日产配置 DTO — 用于参数表管理页面。段落由 3 类配置自动生成（冷轧拔/普通工段/检验），仅参数可编辑。
/// </summary>
public class SectionParagraphConfigDto
{
    public int Id { get; set; }

    /// <summary>段落显示名（冷轧=机台组显示名、普通=工段中文名、检验=固定中文）</summary>
    public string ParagraphName { get; set; } = null!;

    /// <summary>稳定 Key（冷轧=机台组 GroupKey、普通=SectionKey、检验=固定中文）；同步后恒有值</summary>
    public string? ParagraphKey { get; set; }

    /// <summary>段落类别类型（Cold/Section/Fixed）；同步后恒有值</summary>
    public string? CategoryType { get; set; }

    /// <summary>展示序号</summary>
    public int DisplayOrder { get; set; }

    /// <summary>日流转设定（吨/天）</summary>
    public decimal? DailyFlowTarget { get; set; }

    /// <summary>偏少天数值</summary>
    public decimal? LowerLimitDays { get; set; }

    /// <summary>过多天数值</summary>
    public decimal? UpperLimitDays { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}
