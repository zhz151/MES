using MES.Core.Enums;

namespace MES.Core.Constants;

/// <summary>
/// 库存批次物料类型常量（对应 InventoryBatch.MaterialType 字段的合法值，DB 存储枚举名）
/// </summary>
public static class InventoryMaterialTypes
{
    // DB 存储的是枚举名，常量值与枚举名一致
    public const string Finished = "Finished";
    public const string OrderFinished = "OrderFinished";
    public const string CriticalFinished = "CriticalFinished";
    public const string Surplus = "Surplus";
    public const string SemiFinished = "SemiFinished";
    public const string DefectSemi = "DefectSemi";
    public const string DefectFinished = "DefectFinished";
    public const string RoughTube = "RoughTube";
    public const string RoundBar = "RoundBar";
    public const string DefectRoundBar = "DefectRoundBar";
    public const string DefectRoughTube = "DefectRoughTube";
    public const string Scrap = "Scrap";
    public const string SpecialDeliveryStatus = "SpecialDeliveryStatus";
    public const string DefectWIP = "DefectWIP";

    /// <summary>
    /// 库存使用计划可用的物料类型（DB 字符串比较）
    /// </summary>
    public static readonly string[] InventoryPlanUsable = { Finished, Surplus };

    /// <summary>
    /// 空拉/少道次改制可用的物料类型（DB 字符串比较）
    /// </summary>
    public static readonly string[] EmptyDrawingReworkUsable = { Finished, SemiFinished, Surplus, DefectSemi, DefectFinished };

    /// <summary>
    /// 人工选择改制排除的物料类型（DB 字符串比较）
    /// </summary>
    public static readonly string[] ManualSelectReworkExcluded = { RoundBar, DefectRoundBar, DefectRoughTube, Scrap, CriticalFinished, OrderFinished, SpecialDeliveryStatus };

    /// <summary>
    /// 各仓库允许的物料类型白名单
    /// </summary>
    public static readonly Dictionary<string, HashSet<MaterialType>> WarehouseAllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RAW"] =     new() { MaterialType.RoughTube, MaterialType.RoundBar, MaterialType.SemiFinished },
        ["FG"] =      new() { MaterialType.Finished, MaterialType.OrderFinished, MaterialType.CriticalFinished, MaterialType.SpecialDeliveryStatus },
        ["DEFECT"] =  new() { MaterialType.DefectRoundBar, MaterialType.DefectRoughTube, MaterialType.DefectSemi, MaterialType.DefectFinished, MaterialType.DefectWIP, MaterialType.Scrap },
        ["WIP"] =     new() { MaterialType.Surplus },
    };

    /// <summary>
    /// 获取指定仓库代码允许的物料类型集合，未知仓库代码返回 null
    /// </summary>
    public static HashSet<MaterialType>? GetAllowedTypes(string warehouseCode)
    {
        return WarehouseAllowedTypes.TryGetValue(warehouseCode, out var types) ? types : null;
    }
}
