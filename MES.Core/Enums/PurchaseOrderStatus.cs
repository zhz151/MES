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
    /// 超量到货（到料重量 &gt; 采购重量×超额比率 且 超出量 &gt; 超额偏差阈值）
    /// </summary>
    OverReceived
}
