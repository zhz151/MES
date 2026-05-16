namespace MES.Core.Enums;

/// <summary>
/// 维修工单状态（由字段完整度自动推导）
/// </summary>
public enum RepairOrderStatus
{
    /// <summary>
    /// 待维修（已报修）
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 维修中（有开始时间）
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// 完成（有完成时间）
    /// </summary>
    Completed = 2
}
