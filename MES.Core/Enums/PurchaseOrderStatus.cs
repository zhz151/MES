namespace MES.Core.Enums;

/// <summary>
/// 采购订单状态
/// </summary>
public enum PurchaseOrderStatus
{
    /// <summary>
    /// 未到货/开放
    /// </summary>
    Open,

    /// <summary>
    /// 部分到货
    /// </summary>
    Partial,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed,

    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled
}
