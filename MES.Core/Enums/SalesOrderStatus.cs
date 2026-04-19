namespace MES.Core.Enums;

/// <summary>
/// 订单状态（三态）
/// </summary>
public enum SalesOrderStatus
{
    /// <summary>
    /// 待处理（意向合同）
    /// </summary>
    Pending,
    
    /// <summary>
    /// 已确认（合同生效）
    /// </summary>
    Confirmed,
    
    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled
}