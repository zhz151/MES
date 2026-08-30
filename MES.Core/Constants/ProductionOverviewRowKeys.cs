namespace MES.Core.Constants;

/// <summary>
/// 生产总览调度行名（DailyProductionCapacities.ProcessName）英文稳定 Key 常量及双向映射。
/// 存储层与后端匹配一律使用英文 Key。荒管抛光为固定首行（不在机台组体系）；
/// 冷轧/冷拔机台组行由配置表 ColdRollMachineGroupConfig 动态驱动（行 Key=组 GroupKey，
/// 2026-08-30 用户决策：完全遍历机台组含 110）。
/// </summary>
public static class ProductionOverviewRowKeys
{
    // ========== 生产总览行名英文 Key 常量 ==========
    /// <summary>荒管抛光</summary>
    public const string Polish = "Polish";

    /// <summary>所有行名 Key 的有序列表（冷轧/冷拔行由机台组配置动态生成）</summary>
    public static readonly string[] All =
    [
        Polish
    ];

    /// <summary>Key → 规范中文（显示兜底，仅荒管抛光）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Polish] = "荒管抛光",
        };

    private static readonly HashSet<string> KeySet = new(All, StringComparer.Ordinal);

    /// <summary>是否为合法行名 Key（Ordinal）</summary>
    public static bool IsKey(string? value)
        => !string.IsNullOrEmpty(value) && KeySet.Contains(value!);

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
