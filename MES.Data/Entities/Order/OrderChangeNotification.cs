using MES.Core.Enums;

namespace MES.Data.Entities.Order;

/// <summary>
/// 订单变更通知实体
/// </summary>
public class OrderChangeNotification : BaseEntity
{
    /// <summary>
    /// 订单号
    /// </summary>
    public string OrderNumber { get; set; } = null!;

    /// <summary>
    /// 变更类型
    /// </summary>
    public NotificationChangeType ChangeType { get; set; }

    /// <summary>
    /// 清理工单数量（仅删除类型有效）
    /// </summary>
    public int WorkOrderCount { get; set; }

    /// <summary>
    /// 是否已读
    /// </summary>
    public bool IsRead { get; set; }
}