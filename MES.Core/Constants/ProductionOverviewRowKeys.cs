namespace MES.Core.Constants;

/// <summary>
/// 生产总览调度行名（DailyProductionCapacities.ProcessName）英文稳定 Key 常量及双向映射。
/// 存储层与后端匹配一律使用英文 Key（如 "Mill50_60"），显示层使用中文（50,60轧机/荒管抛光…）。
/// 行名来自重点工序日产能力配置表（DailyProductionCapacities），用户可改名 → 属配置字典化。
/// </summary>
public static class ProductionOverviewRowKeys
{
    // ========== 5 个生产总览行名英文 Key 常量 ==========
    /// <summary>荒管抛光</summary>
    public const string Polish = "Polish";

    /// <summary>50,60轧机</summary>
    public const string Mill50_60 = "Mill50_60";

    /// <summary>20,30轧机</summary>
    public const string Mill20_30 = "Mill20_30";

    /// <summary>三辊轧机</summary>
    public const string ThreeRollMill = "ThreeRollMill";

    /// <summary>拉机</summary>
    public const string DrawBench = "DrawBench";

    /// <summary>所有行名 Key 的有序列表</summary>
    public static readonly string[] All =
    [
        Polish, Mill50_60, Mill20_30, ThreeRollMill, DrawBench
    ];

    /// <summary>Key → 规范中文（显示兜底）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Polish] = "荒管抛光",
            [Mill50_60] = "50,60轧机",
            [Mill20_30] = "20,30轧机",
            [ThreeRollMill] = "三辊轧机",
            [DrawBench] = "拉机",
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
