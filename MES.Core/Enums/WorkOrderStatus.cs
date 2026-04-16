namespace MES.Core.Enums;

/// <summary>
/// 工单状态枚举
/// </summary>
public enum WorkOrderStatus
{
    /// <summary>
    /// 待处理
    /// </summary>
    Pending,
    
    /// <summary>
    /// 处理中
    /// </summary>
    Processing,
    
    /// <summary>
    /// 已完成
    /// </summary>
    Completed,
    
    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled
}