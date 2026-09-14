namespace MES.Core.Enums;

/// <summary>
/// 不合格报告「待处理批次」来源类型 — 仅供待处理列表展示与来源跳转使用。
/// 与工位报工模板类型 <see cref="ReportTemplateType"/> 无关（后者不得扩展）。
/// </summary>
public enum NcrPendingSourceType
{
    /// <summary>过程检验（缺陷数/占比超阈值）</summary>
    ProcessInspection,

    /// <summary>成品检验（缺陷数/占比超阈值）</summary>
    FinalInspection,

    /// <summary>不合格反馈（人工上报，不受阈值约束，无条件列出）</summary>
    NonconformingFeedback
}
