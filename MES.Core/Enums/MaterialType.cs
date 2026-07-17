namespace MES.Core.Enums;

/// <summary>
/// 物料类型（对应 InventoryBatch.MaterialType / ProductionBatch.SourceMaterialType）
/// </summary>
public enum MaterialType
{
    /// <summary>备料成品</summary>
    Finished,
    /// <summary>订单成品</summary>
    OrderFinished,
    /// <summary>临界成品</summary>
    CriticalFinished,
    /// <summary>余库料</summary>
    Surplus,
    /// <summary>半成品</summary>
    SemiFinished,
    /// <summary>次品半成品</summary>
    DefectSemi,
    /// <summary>次品成品</summary>
    DefectFinished,
    /// <summary>荒管</summary>
    RoughTube,
    /// <summary>圆棒</summary>
    RoundBar,
    /// <summary>次品圆棒</summary>
    DefectRoundBar,
    /// <summary>次品荒管</summary>
    DefectRoughTube,
    /// <summary>报废品</summary>
    Scrap,
    /// <summary>特定交态成品</summary>
    SpecialDeliveryStatus,
    /// <summary>在制品</summary>
    WorkInProgress,
    /// <summary>次品在制</summary>
    DefectWIP
}
