namespace MES.Core.Constants;

/// <summary>
/// 生产流转性英文稳定 Key 常量及双向映射。存储层与后端匹配一律使用英文 Key（如 "Normal"），
/// 显示层使用中文（正常/暂停/待料/疑问/略）。由 WorkOrderExecutionService 按工单状态/催单/流转缺口计算产生，
/// null = 使用系统值（WorkOrderPlan 覆盖表语义），属固定五值状态机（枚举化，非配置字典）。
/// </summary>
public static class ProductionFlowKeys
{
    // ========== 5 个流转性英文 Key 常量 ==========
    /// <summary>正常（在产/催单推进中）</summary>
    public const string Normal = "Normal";

    /// <summary>暂停（工单暂停）</summary>
    public const string Paused = "Paused";

    /// <summary>待料（原料锁定阶段）</summary>
    public const string Waiting = "Waiting";

    /// <summary>疑问（有未完成批次或异常）</summary>
    public const string Doubt = "Doubt";

    /// <summary>略（无关注价值，已闭环）</summary>
    public const string Skip = "Skip";

    /// <summary>所有流转性 Key 的有序列表</summary>
    public static readonly string[] All =
    [
        Normal, Paused, Waiting, Doubt, Skip
    ];

    /// <summary>Key → 规范中文（显示兜底）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Normal] = "正常",
            [Paused] = "暂停",
            [Waiting] = "待料",
            [Doubt] = "疑问",
            [Skip] = "略",
        };

    /// <summary>规范中文 → Key（迁移前存量归一用）</summary>
    public static readonly IReadOnlyDictionary<string, string> ChineseToKey =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["正常"] = Normal,
            ["暂停"] = Paused,
            ["待料"] = Waiting,
            ["疑问"] = Doubt,
            ["略"] = Skip,
        };

    private static readonly HashSet<string> KeySet = new(All, StringComparer.Ordinal);

    /// <summary>是否为合法流转性 Key（Ordinal）</summary>
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
