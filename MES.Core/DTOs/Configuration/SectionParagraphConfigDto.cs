namespace MES.Core.DTOs.Configuration;

/// <summary>
/// 段落日产配置 DTO — 用于参数表管理页面。段落包含的(工序组,工段,产类)组合由组合归类表 CombinationGroups 的「归属段落」承载。
/// </summary>
public class SectionParagraphConfigDto
{
    public int Id { get; set; }

    /// <summary>段落类别（中文）</summary>
    public string ParagraphName { get; set; } = null!;

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
