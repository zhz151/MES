using System.Collections.Generic;

namespace MES.Core.Constants;

/// <summary>
/// 岗位英文稳定 Key 常量及双向映射（员工 Position 字段固定化）。
/// 存储层与后端匹配一律使用英文 Key（如 "Cutting"），显示层使用中文。
/// 岗位可扩展：DictValueDefinition 字典（DictKey=PositionKey）可加值/改名/隐藏，
/// 本类仅提供 14 个存量岗位的静态兜底映射（配置表优先 → 本类兜底）。
/// </summary>
public static class PositionKeys
{
    // ========== 14 个岗位英文 Key 常量 ==========
    /// <summary>成品检验</summary>
    public const string FinishedInspection = "FinishedInspection";

    /// <summary>酸洗</summary>
    public const string AcidWashing = "AcidWashing";

    /// <summary>高速轧机</summary>
    public const string HighSpeedMill = "HighSpeedMill";

    /// <summary>矫直</summary>
    public const string Straightening = "Straightening";

    /// <summary>切割</summary>
    public const string Cutting = "Cutting";

    /// <summary>生产后勤</summary>
    public const string ProductionLogistics = "ProductionLogistics";

    /// <summary>修磨</summary>
    public const string Grinding = "Grinding";

    /// <summary>污水处理</summary>
    public const string SewageTreatment = "SewageTreatment";

    /// <summary>固溶</summary>
    public const string Solution = "Solution";

    /// <summary>60冷轧</summary>
    public const string ColdRoll60 = "ColdRoll60";

    /// <summary>办公室</summary>
    public const string Office = "Office";

    /// <summary>材料仓库</summary>
    public const string MaterialWarehouse = "MaterialWarehouse";

    /// <summary>生产车间</summary>
    public const string ProductionWorkshop = "ProductionWorkshop";

    /// <summary>轧拉机</summary>
    public const string RollingDrawing = "RollingDrawing";

    /// <summary>所有岗位 Key 的有序列表（顺序同存量岗位出现频次降序）</summary>
    public static readonly string[] All =
    [
        FinishedInspection, AcidWashing, HighSpeedMill, Straightening, Cutting, ProductionLogistics,
        Grinding, SewageTreatment, Solution, ColdRoll60, Office, MaterialWarehouse, ProductionWorkshop, RollingDrawing
    ];

    /// <summary>Key → 规范中文（显示兜底）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese = new Dictionary<string, string>
    {
        [FinishedInspection] = "成品检验",
        [AcidWashing] = "酸洗",
        [HighSpeedMill] = "高速轧机",
        [Straightening] = "矫直",
        [Cutting] = "切割",
        [ProductionLogistics] = "生产后勤",
        [Grinding] = "修磨",
        [SewageTreatment] = "污水处理",
        [Solution] = "固溶",
        [ColdRoll60] = "60冷轧",
        [Office] = "办公室",
        [MaterialWarehouse] = "材料仓库",
        [ProductionWorkshop] = "生产车间",
        [RollingDrawing] = "轧拉机",
    };

    /// <summary>规范中文 → Key。程序识别输入归一用（搜索/导入）。</summary>
    public static readonly IReadOnlyDictionary<string, string> ChineseToKey = BuildChineseToKey();

    /// <summary>所有 Key 的集合（Ordinal 比较，供 IsKey 快速判定）</summary>
    private static readonly HashSet<string> KeySet = new(All, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> BuildChineseToKey()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in KeyToChinese)
            map[kvp.Value] = kvp.Key;
        return map;
    }

    /// <summary>是否为合法英文 Key（Ordinal）</summary>
    public static bool IsKey(string? value)
        => !string.IsNullOrEmpty(value) && KeySet.Contains(value!);

    /// <summary>归一为稳定 Key：已是 Key 原样返回；规范中文反查；未知返回 null。</summary>
    public static string? ToKey(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (KeySet.Contains(value)) return value;
        return ChineseToKey.TryGetValue(value, out var key) ? key : null;
    }

    /// <summary>归一为显示中文：Key → 规范中文；已是中文原样返回（兼容存量）；未知返回 null。</summary>
    public static string? ToChinese(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (KeyToChinese.TryGetValue(value, out var cn)) return cn;
        return value;
    }
}
