namespace MES.Core.Enums;

/// <summary>
/// 工段可视化状态
/// </summary>
public enum SectionStatus
{
    /// <summary>已完成</summary>
    Completed,

    /// <summary>进行中</summary>
    InProgress,

    /// <summary>委外中</summary>
    Outsource,

    /// <summary>下一个待执行</summary>
    Next,

    /// <summary>待处理</summary>
    Pending
}
