using System.Collections.Generic;

namespace MES.Core.Constants;

/// <summary>
/// NCR 责任类别英文稳定 Key 常量及双向映射。存储层与后端匹配一律使用英文 Key
/// （如 "ProductionInternal"），显示层使用中文（生产-厂内/生产-外协/原料-荒管/原料-外购成品/原料-余库料，
/// 可经配置表 DictValueDefinitions（DictKey=NcrResponsibilityKey）改名）。
/// 属可扩展配置字典：用户在配置表可新增责任类别（Key 固定、Name 可改）。
/// 内置 Key 沿用原 ResponsibilityCategory 枚举名，存量 NCR 数据零迁移。
/// </summary>
public static class NcrResponsibilityKeys
{
    // ========== 内置责任类别英文 Key 常量 ==========
    /// <summary>生产-厂内</summary>
    public const string ProductionInternal = "ProductionInternal";

    /// <summary>生产-外协</summary>
    public const string ProductionOutsource = "ProductionOutsource";

    /// <summary>原料-荒管</summary>
    public const string MaterialTubeBlank = "MaterialTubeBlank";

    /// <summary>原料-外购成品</summary>
    public const string MaterialPurchased = "MaterialPurchased";

    /// <summary>原料-余库料</summary>
    public const string MaterialSurplus = "MaterialSurplus";

    /// <summary>所有内置责任类别 Key 的有序列表</summary>
    public static readonly string[] All =
    [
        ProductionInternal, ProductionOutsource, MaterialTubeBlank, MaterialPurchased, MaterialSurplus
    ];

    /// <summary>Key → 规范中文（显示兜底）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProductionInternal] = "生产-厂内",
            [ProductionOutsource] = "生产-外协",
            [MaterialTubeBlank] = "原料-荒管",
            [MaterialPurchased] = "原料-外购成品",
            [MaterialSurplus] = "原料-余库料",
        };

    /// <summary>规范中文 → Key（迁移前存量归一用）</summary>
    public static readonly IReadOnlyDictionary<string, string> ChineseToKey =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["生产-厂内"] = ProductionInternal,
            ["生产-外协"] = ProductionOutsource,
            ["原料-荒管"] = MaterialTubeBlank,
            ["原料-外购成品"] = MaterialPurchased,
            ["原料-余库料"] = MaterialSurplus,
        };

    private static readonly HashSet<string> KeySet = new(All, StringComparer.Ordinal);

    /// <summary>是否为合法责任类别 Key（Ordinal）</summary>
    public static bool IsKey(string? value)
        => !string.IsNullOrEmpty(value) && KeySet.Contains(value!);

    /// <summary>
    /// 归一为稳定 Key：已是 Key 原样返回；中文反查；未知返回 null。
    /// </summary>
    public static string? ToKey(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (KeySet.Contains(value)) return value;
        return ChineseToKey.TryGetValue(value, out var key) ? key : null;
    }

    /// <summary>
    /// 归一为显示中文：Key → 中文；已是中文（迁移前存量）原样返回；未知返回 null。
    /// </summary>
    public static string? ToChinese(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (KeyToChinese.TryGetValue(value, out var cn)) return cn;
        // 已是中文（迁移前存量）或未知值：原样返回
        return value;
    }
}
