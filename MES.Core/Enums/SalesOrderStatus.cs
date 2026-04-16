namespace MES.Core.Enums;

/// <summary>
/// 订单状态枚举
/// </summary>
public enum SalesOrderStatus
{
    /// <summary>
    /// 待处理
    /// </summary>
    Pending,
    
    /// <summary>
    /// 已确认
    /// </summary>
    Confirmed,
    
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