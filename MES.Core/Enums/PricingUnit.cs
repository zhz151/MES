namespace MES.Core.Enums;

/// <summary>
/// 订单项次计价单位（决定总价取量：元/Kg→合同重量、元/米→米数、元/支→支数）
/// </summary>
public enum PricingUnit
{
    /// <summary>元/Kg（按合同重量计）</summary>
    PerKg,
    /// <summary>元/米（按米数计）</summary>
    PerMeter,
    /// <summary>元/支（按支数计）</summary>
    PerPiece
}
