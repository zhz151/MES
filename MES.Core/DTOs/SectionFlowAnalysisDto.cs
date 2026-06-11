namespace MES.Core.DTOs;

/// <summary>
/// 生产段流转量分析 DTO — 按 12 个段落类别汇总
/// </summary>
public class SectionFlowAnalysisDto
{
    public int Id { get; set; }
    public string CategoryCode { get; set; } = null!;
    public string CategoryName { get; set; } = null!;

    /// <summary>段落待产总量（计算值）</summary>
    public decimal? PendingTotal { get; set; }

    /// <summary>变异量总量（计算值）</summary>
    public decimal? VariationTotal { get; set; }

    /// <summary>变异量预算日产（可编辑）</summary>
    public decimal? DailyProductionTarget { get; set; }

    /// <summary>可持续天数（计算值）</summary>
    public decimal? SustainableDays { get; set; }

    /// <summary>偏少天数值（可编辑）</summary>
    public decimal? LowerLimitDays { get; set; }

    /// <summary>过多天数值（可编辑）</summary>
    public decimal? UpperLimitDays { get; set; }

    /// <summary>状态判定：偏少/正常/过多（计算值）</summary>
    public string? StatusJudgment { get; set; }
}
