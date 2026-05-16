namespace MES.Core.Enums;

/// <summary>
/// 点检/保养工单共用状态
/// </summary>
public enum TaskOrderStatus
{
    /// <summary>
    /// 待执行
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed = 1,

    /// <summary>
    /// 已逾期
    /// </summary>
    Overdue = 2
}
