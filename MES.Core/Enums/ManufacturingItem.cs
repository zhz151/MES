namespace MES.Core.Enums;

/// <summary>
/// 制造物品
/// </summary>
public enum ManufacturingItem
{
    /// <summary>订单成品</summary>
    OrderFinishedProduct,
    /// <summary>备料成品</summary>
    PreparedMaterial,
    /// <summary>余库料</summary>
    SurplusStock,
    /// <summary>中间品</summary>
    IntermediateProduct,
    /// <summary>特定交态成品</summary>
    SpecialDeliveryStatus
}
