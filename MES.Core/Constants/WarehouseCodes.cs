namespace MES.Core.Constants;

/// <summary>
/// 仓库代码常量（与 DbInitializer 种子数据、InventoryMaterialTypes.WarehouseAllowedTypes 键一致）
/// </summary>
public static class WarehouseCodes
{
    /// <summary>原料库（荒管/圆钢存放）</summary>
    public const string Raw = "RAW";

    /// <summary>成品库（成品管存放）</summary>
    public const string FinishedGoods = "FG";

    /// <summary>次品库（次品/不合格品存放）</summary>
    public const string Defect = "DEFECT";

    /// <summary>在制品库（在制品/半成品存放，实际物料类型为余库料 Surplus）</summary>
    public const string WorkInProgress = "WIP";
}
