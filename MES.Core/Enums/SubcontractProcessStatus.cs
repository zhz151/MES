namespace MES.Core.Enums;

/// <summary>
/// 委外加工状态
/// </summary>
public enum SubcontractProcessStatus
{
    /// <summary>
    /// 待回收
    /// </summary>
    Pending,

    /// <summary>
    /// 部分回收
    /// </summary>
    PartialReturned,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed
}
