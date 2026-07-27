namespace MES.Core.Enums;

/// <summary>
/// 成品类型（外购成品计划）
/// </summary>
public enum FinishedProductType
{
    /// <summary>
    /// 临界成品（需要回厂复检）
    /// </summary>
    Critical = 1,

    /// <summary>
    /// 订单成品（不需再检验）
    /// </summary>
    Order = 2,

    /// <summary>
    /// 订成-非交付态
    /// </summary>
    SpecialDeliveryStatus = 3
}
