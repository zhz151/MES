using MES.Core.Enums;

namespace MES.Data.Entities;

/// <summary>
/// 工单实体（订单上下文所需的最小定义）
/// </summary>
public class WorkOrder : BaseEntity
{
    /// <summary>
    /// 工单号，唯一索引
    /// </summary>
    public string OrderNo { get; set; } = null!;

    /// <summary>
    /// 订单项次ID，外键到OrderItem
    /// </summary>
    public int OrderItemId { get; set; }

    /// <summary>
    /// 工单状态
    /// </summary>
    public WorkOrderStatus Status { get; set; }

    /// <summary>
    /// 优先级（数字越小越高）
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 计划开始日期
    /// </summary>
    public DateTime? PlannedStartDate { get; set; }

    /// <summary>
    /// 计划结束日期
    /// </summary>
    public DateTime? PlannedEndDate { get; set; }

    /// <summary>
    /// 实际开始日期
    /// </summary>
    public DateTime? ActualStartDate { get; set; }

    /// <summary>
    /// 实际结束日期
    /// </summary>
    public DateTime? ActualEndDate { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 导航属性 - 订单项次
    /// </summary>
    public OrderItem OrderItem { get; set; } = null!;

    /// <summary>
    /// 导航属性 - 订单项次（反向关联）
    /// </summary>
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}