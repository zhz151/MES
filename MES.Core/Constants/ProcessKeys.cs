namespace MES.Core.Constants;

/// <summary>
/// 工序组英文稳定 Key 常量及双向映射。存储层与后端匹配一律使用英文 Key（如 "ColdRoll60"），
/// 显示层使用中文（ProcessNames 中文常量或配置表 ProcessDefinition.ProcessName）。
/// 与 <see cref="ProcessNames"/> 一一对应：ProcessNames 值是中文（显示契约），
/// ProcessKeys 值是与常量名一致的英文 Key（程序识别契约），两者通过本类双向转换。
/// </summary>
public static class ProcessKeys
{
    // ========== 9 个工序组英文 Key 常量（与 ProcessNames 常量名一致） ==========
    public const string RoughTubeProcessing = "RoughTubeProcessing";
    public const string InProcessRepair = "InProcessRepair";
    public const string ColdRoll60 = "ColdRoll60";
    public const string ColdRoll50 = "ColdRoll50";
    public const string ColdRoll30 = "ColdRoll30";
    public const string ColdRoll20 = "ColdRoll20";
    public const string ThreeRollColdRoll = "ThreeRollColdRoll";
    public const string ColdDraw = "ColdDraw";
    public const string AdditionalFinalInspection = "AdditionalFinalInspection";

    /// <summary>所有工序组 Key 的有序列表（顺序同 ProcessNames.All）</summary>
    public static readonly string[] All =
    [
        RoughTubeProcessing, InProcessRepair, ColdRoll60, ColdRoll50,
        ColdRoll30, ColdRoll20, ThreeRollColdRoll, ColdDraw,
        AdditionalFinalInspection
    ];

    /// <summary>Key → 规范中文（ProcessNames.PropertyToName，显示兜底）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese =
        ProcessNames.PropertyToName;

    /// <summary>规范中文（含别名）→ Key。程序识别输入归一用。</summary>
    public static readonly IReadOnlyDictionary<string, string> ChineseToKey = BuildChineseToKey();

    /// <summary>所有 Key 的集合（Ordinal 比较，供 IsKey 快速判定）</summary>
    private static readonly HashSet<string> KeySet = new(All, StringComparer.Ordinal);

    /// <summary>冷轧类工序 Key 集（五档冷轧，不含冷拔），Ordinal 比较</summary>
    private static readonly HashSet<string> ColdRollKeySet = new(
        new[] { ColdRoll60, ColdRoll50, ColdRoll30, ColdRoll20, ThreeRollColdRoll },
        StringComparer.Ordinal);

    /// <summary>是否为冷轧类工序（五档冷轧）</summary>
    public static bool IsColdRoll(string? value)
        => !string.IsNullOrEmpty(value) && ColdRollKeySet.Contains(value!);

    /// <summary>是否为冷轧或冷拔类工序（需冷轧拔工段）</summary>
    public static bool IsColdRollOrColdDraw(string? value)
        => IsColdRoll(value) || value == ColdDraw;

    private static IReadOnlyDictionary<string, string> BuildChineseToKey()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        // 规范中文 → Key（反查 PropertyToName）
        foreach (var kvp in ProcessNames.PropertyToName)
        {
            map[kvp.Value] = kvp.Key;
        }
        // 别名 → 规范 Key（Aliases：别名中文 → 规范中文）
        foreach (var kvp in ProcessNames.Aliases)
        {
            if (map.TryGetValue(kvp.Value, out var key))
                map[kvp.Key] = key;
        }
        return map;
    }

    /// <summary>是否为合法英文 Key（Ordinal）</summary>
    public static bool IsKey(string? value)
        => !string.IsNullOrEmpty(value) && KeySet.Contains(value!);

    /// <summary>
    /// 归一为稳定 Key：已是 Key 原样返回；规范中文/别名反查；未知返回 null。
    /// </summary>
    public static string? ToKey(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (KeySet.Contains(value)) return value;
        return ChineseToKey.TryGetValue(value, out var key) ? key : null;
    }

    /// <summary>
    /// 归一为显示中文：Key → 规范中文；已是中文（规范/别名）原样返回（兼容迁移前存量）；未知返回 null。
    /// </summary>
    public static string? ToChinese(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (ProcessNames.PropertyToName.TryGetValue(value, out var cn)) return cn;
        // 已是中文（含别名）或未知值：原样返回
        return value;
    }
}
