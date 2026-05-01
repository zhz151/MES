namespace MES.Core.Enums;

/// <summary>
/// 物料分类枚举
/// </summary>
public enum MaterialCategory
{
    /// <summary>原材料</summary>
    RoundBar = 0,

    /// <summary>二级原料</summary>
    RoughTube = 1,

    /// <summary>半成品</summary>
    SemiProduct = 2,

    /// <summary>订单对应的成品</summary>
    OrderFinished = 3,

    /// <summary>非订单成品</summary>
    StockFinished = 4,

    /// <summary>与订单规格一致但状态可能不同</summary>
    CriticalFinished = 5,

    /// <summary>不合格圆棒</summary>
    DefectRoundBar = 6,

    /// <summary>不合格荒管</summary>
    DefectRoughTube = 7,

    /// <summary>不合格中间品</summary>
    DefectSemiProduct = 8,

    /// <summary>不合格成品</summary>
    DefectFinished = 9,

    /// <summary>无法返修</summary>
    Scrap = 10,

    /// <summary>生产过程多余料</summary>
    Surplus = 11
}
