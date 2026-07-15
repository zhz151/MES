namespace MES.Core.Enums;

/// <summary>
/// 物料分类枚举
/// </summary>
public enum MaterialCategory
{
    /// <summary>圆棒</summary>
    RoundBar = 0,

    /// <summary>荒管</summary>
    RoughTube = 1,

    /// <summary>半成品</summary>
    SemiProduct = 2,

    /// <summary>订单对应的成品</summary>
    OrderFinished = 3,

    /// <summary>备料成品</summary>
    PreparedFinished = 4,

    /// <summary>与订单规格一致但状态可能不同</summary>
    CriticalFinished = 5,

    /// <summary>次品圆棒</summary>
    DefectRoundBar = 6,

    /// <summary>次品荒管</summary>
    DefectRoughTube = 7,

    /// <summary>次品半成品</summary>
    DefectSemiProduct = 8,

    /// <summary>次品成品</summary>
    DefectFinished = 9,

    /// <summary>无法返修</summary>
    Scrap = 10,

    /// <summary>生产过程多余料</summary>
    Surplus = 11,

    /// <summary>特定交态成品</summary>
    SpecialDeliveryFinished = 12,

    /// <summary>次品在制</summary>
    DefectWIP = 13
}
