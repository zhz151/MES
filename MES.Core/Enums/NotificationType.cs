namespace MES.Core.Enums;

/// <summary>
/// 通知类型枚举
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// 新物料待审核
    /// </summary>
    NewMaterial,

    /// <summary>
    /// 删除被阻止
    /// </summary>
    DeleteBlocked,

    /// <summary>
    /// 出库预警
    /// </summary>
    OutboundAlert,

    /// <summary>
    /// 工单已删除
    /// </summary>
    WorkOrderDeleted,

    /// <summary>
    /// 订单已删除
    /// </summary>
    OrderDeleted,

    /// <summary>
    /// 订单已变更
    /// </summary>
    OrderChanged
}
