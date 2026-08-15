namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 生产段落流转量分析 DTO — 按生产段落汇总（组合归类表「归属段落」三维行上卷）
/// </summary>
public class SectionParagraphFlowAnalysisDto
{
    /// <summary>段落配置 Id（SectionParagraphConfig.Id）</summary>
    public int Id { get; set; }

    /// <summary>生产段落（中文段落名）</summary>
    public string ParagraphName { get; set; } = null!;

    /// <summary>展示序号（配置表 DisplayOrder，前端排序键）</summary>
    public int DisplayOrder { get; set; }

    /// <summary>待在产重量（计算值，吨）</summary>
    public decimal? PendingTotal { get; set; }

    /// <summary>变异量总量（计算值）</summary>
    public decimal? VariationTotal { get; set; }

    /// <summary>日流转设定（可编辑）</summary>
    public decimal? DailyFlowTarget { get; set; }

    /// <summary>可持续天数（计算值）</summary>
    public decimal? SustainableDays { get; set; }

    /// <summary>偏少天数值（可编辑）</summary>
    public decimal? LowerLimitDays { get; set; }

    /// <summary>过多天数值（可编辑）</summary>
    public decimal? UpperLimitDays { get; set; }

    /// <summary>状态判定：偏少/正常/过多（计算值，前端显示名"总况判定"）</summary>
    public string? StatusJudgment { get; set; }

    /// <summary>重点批次计数（来自批次计划，按(待产工序组, 工段, 产类)匹配组合归类「归属段落」上卷）</summary>
    public int KeyBatchCount { get; set; }

    /// <summary>重点批次重量（来自批次计划）</summary>
    public decimal? KeyBatchWeight { get; set; }

    /// <summary>计划流转量（批次计划中流转=是的重量汇总，吨）</summary>
    public decimal? PlanFlowQuantity { get; set; }

    /// <summary>计划流转判定：计划流转量 &gt; 日流转设定 → 加速，否则 -</summary>
    public string? PlanFlowJudgment { get; set; }

    /// <summary>重点批重量（批次计划中流转=是 且 等级=急+ 的重量汇总，吨）</summary>
    public decimal? PlanKeyWeight { get; set; }
}
