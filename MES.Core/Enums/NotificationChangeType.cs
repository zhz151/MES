namespace MES.Core.Enums;

/// <summary>
/// 通知变更类型
/// </summary>
public enum NotificationChangeType
{
    /// <summary>
    /// 订单删除（自动清理工单）
    /// </summary>
    Deleted = 0,

    /// <summary>
    /// 订单项次变更
    /// </summary>
    ItemChanged = 1
}