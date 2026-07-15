namespace MES.Core.Constants;

/// <summary>
/// 库存批次物料类型常量（对应 InventoryBatch.MaterialType 字段的合法值）
/// </summary>
public static class InventoryMaterialTypes
{
    public const string Finished = "备料成品";
    public const string OrderFinished = "订单成品";
    public const string Surplus = "余库料";
    public const string SemiFinished = "中间品";
    public const string DefectSemi = "次品中间品";
    public const string DefectFinished = "次品成品";
    public const string RoundBar = "圆棒";
    public const string DefectRoundBar = "次品圆棒";
    public const string DefectRoughTube = "次品荒管";
    public const string Scrap = "报废品";
    public const string SpecialDeliveryStatus = "特定交态成品";

    /// <summary>
    /// 库存使用计划可用的物料类型
    /// </summary>
    public static readonly string[] InventoryPlanUsable = { Finished, Surplus, SpecialDeliveryStatus };

    /// <summary>
    /// 空拉/少道次改制可用的物料类型
    /// </summary>
    public static readonly string[] EmptyDrawingReworkUsable = { Finished, SemiFinished, Surplus, DefectSemi, DefectFinished };

    /// <summary>
    /// 人工选择改制排除的物料类型
    /// </summary>
    public static readonly string[] ManualSelectReworkExcluded = { RoundBar, DefectRoundBar, DefectRoughTube, Scrap };
}
