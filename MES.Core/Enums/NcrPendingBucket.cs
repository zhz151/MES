namespace MES.Core.Enums;

/// <summary>
/// 不合格报告「待处理批次」分组 —— 由来源类型派生，仅供列表/建单页分组展示。
/// NormalSubmitted = 人工上报的不合格反馈（不受阈值约束，无条件列出）；
/// OverageMissing  = 过程检验/成品检验超阈值但无人反馈，系统反查兜底列出。
/// </summary>
public enum NcrPendingBucket
{
    /// <summary>正常提交（来源=不合格反馈，人工上报）</summary>
    NormalSubmitted,

    /// <summary>超阈值遗漏（来源=过程检验/成品检验，超阈值未反馈）</summary>
    OverageMissing
}
