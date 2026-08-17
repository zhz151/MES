namespace MES.Core.Constants;

/// <summary>
/// 工艺卡打印列布局兜底常量：无配置行的字段列宽权重（前后端共用单一来源）。
/// 有配置（数据库 ProcessCardColumnDefinitions）时以后端读库为准，无配置行走此兜底。
/// </summary>
public static class ProcessCardLayoutDefaults
{
    /// <summary>窄列工段 Key 集合（默认权重 1）——现预置工段全为窄列（冷轧拔→入库）</summary>
    public static readonly HashSet<string> NarrowSectionKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ColdRollDraw", "OilPipeCut", "Degrease", "EmulsionWash", "UltrasonicWash", "ClothPolish",
        "BrightAnnealing", "Solution", "Straighten", "Cut", "ThicknessMeasure", "Pickle",
        "OuterPolish", "InnerPolish", "InnerGrinding", "OuterSpotGrinding", "SandBlasting",
        "ShotBlasting", "Inspection", "WeldingHead", "Welding", "Lubrication", "Packing",
        "Warehouse", "Extra1", "Extra2"
    };

    /// <summary>
    /// 无配置行字段的兜底权重（对齐原 ProcessCardPrintHelper 硬编码逻辑）。
    /// 工序组：备注/工序名称/制造规格=4、窄工段=1、其余描述字段=3；非工序组默认 3。
    /// </summary>
    public static int GetDefaultWeight(string blockKey, string fieldKey)
    {
        if (blockKey == "ProcessGroup")
        {
            if (fieldKey.Equals("Remark", StringComparison.OrdinalIgnoreCase)
                || fieldKey.Equals("ProcessName", StringComparison.OrdinalIgnoreCase)
                || fieldKey.Equals("ManufacturingSpec", StringComparison.OrdinalIgnoreCase))
                return 4;
            return NarrowSectionKeys.Contains(fieldKey) ? 1 : 3;
        }
        return 3;
    }
}
