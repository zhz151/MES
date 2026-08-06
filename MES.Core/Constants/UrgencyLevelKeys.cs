namespace MES.Core.Constants;

/// <summary>
/// 紧急性等级英文稳定 Key 常量及双向映射。存储层与后端匹配一律使用英文 Key（如 "APlusUrgent"），
/// 显示层使用中文（A+急/A急/B顺/C缓/D缓/E停）。由 WorkOrderExecutionService 按剩余工量/交期差阈值计算产生，
/// 属固定六值状态机（枚举化，非配置字典）。
/// </summary>
public static class UrgencyLevelKeys
{
    // ========== 6 个紧急性英文 Key 常量 ==========
    /// <summary>A+急</summary>
    public const string APlusUrgent = "APlusUrgent";

    /// <summary>A急</summary>
    public const string AUrgent = "AUrgent";

    /// <summary>B顺</summary>
    public const string BOrder = "BOrder";

    /// <summary>C缓</summary>
    public const string CSlow = "CSlow";

    /// <summary>D缓</summary>
    public const string DSlow = "DSlow";

    /// <summary>E停（暂停工单覆盖）</summary>
    public const string EPaused = "EPaused";

    /// <summary>所有紧急性 Key 的有序列表</summary>
    public static readonly string[] All =
    [
        APlusUrgent, AUrgent, BOrder, CSlow, DSlow, EPaused
    ];

    /// <summary>Key → 规范中文（显示兜底）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [APlusUrgent] = "A+急",
            [AUrgent] = "A急",
            [BOrder] = "B顺",
            [CSlow] = "C缓",
            [DSlow] = "D缓",
            [EPaused] = "E停",
        };

    /// <summary>规范中文 → Key（迁移前存量归一用）</summary>
    public static readonly IReadOnlyDictionary<string, string> ChineseToKey =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["A+急"] = APlusUrgent,
            ["A急"] = AUrgent,
            ["B顺"] = BOrder,
            ["C缓"] = CSlow,
            ["D缓"] = DSlow,
            ["E停"] = EPaused,
        };

    private static readonly HashSet<string> KeySet = new(All, StringComparer.Ordinal);

    /// <summary>是否为合法紧急性 Key（Ordinal）</summary>
    public static bool IsKey(string? value)
        => !string.IsNullOrEmpty(value) && KeySet.Contains(value!);

    /// <summary>是否为特急（A+急 或 A急，IsKeyBatch/特急计数/特急档判定用）</summary>
    public static bool IsUrgent(string? value)
        => value == APlusUrgent || value == AUrgent;

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
