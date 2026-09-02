using System.Collections.Generic;

namespace MES.Core.Constants;

/// <summary>
/// 岗位类别英文稳定 Key 常量及双向映射（员工 Department 字段字典化，列显示「岗位类别」）。
/// 存储层与后端匹配一律使用英文 Key（如 "Workshop"），显示层使用中文。
/// 岗位类别可扩展：DictValueDefinition 字典（DictKey=PositionCategoryKey）可加值/改名/隐藏，
/// 本类仅提供 4 个存量类别的静态兜底映射（配置表优先 → 本类兜底）。
/// 集体计件将按岗位类别切分「岗位工资」→ 岗位内再按出勤+月度评分分配。
/// </summary>
public static class PositionCategoryKeys
{
    // ========== 4 个岗位类别英文 Key 常量 ==========
    /// <summary>车间生产</summary>
    public const string Workshop = "Workshop";

    /// <summary>质检</summary>
    public const string QualityInspection = "QualityInspection";

    /// <summary>生产后勤</summary>
    public const string ProductionLogistics = "ProductionLogistics";

    /// <summary>生技部</summary>
    public const string Technology = "Technology";

    /// <summary>所有岗位类别 Key 的有序列表（顺序同存量值数量降序）</summary>
    public static readonly string[] All =
    [
        Workshop, QualityInspection, ProductionLogistics, Technology
    ];

    /// <summary>Key → 规范中文（显示兜底）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese = new Dictionary<string, string>
    {
        [Workshop] = "车间生产",
        [QualityInspection] = "质检",
        [ProductionLogistics] = "生产后勤",
        [Technology] = "生技部",
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
