namespace MES.Core.Enums;

/// <summary>
/// 委外加工单状态
/// </summary>
public enum SubcontractOrderStatus
{
    /// <summary>
    /// 已发出未收回
    /// </summary>
    Sent,

    /// <summary>
    /// 部分收回
    /// </summary>
    PartialReturned,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed,

    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled
}
