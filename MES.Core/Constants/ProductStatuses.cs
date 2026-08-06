namespace MES.Core.Constants;

/// <summary>
/// 产类英文稳定 Key 常量及双向映射。存储层与后端匹配一律使用英文 Key（如 "Finished"），
/// 显示层使用中文（荒管/在制/成品）。由 <see cref="MES.Services.Helpers.ProductStatusHelper.Calculate"/>
/// 计算产生，属固定三值状态机（枚举化，非配置字典）。
/// </summary>
public static class ProductStatuses
{
    // ========== 3 个产类英文 Key 常量 ==========
    /// <summary>荒管</summary>
    public const string RoughTube = "RoughTube";

    /// <summary>在制</summary>
    public const string InProgress = "InProgress";

    /// <summary>成品</summary>
    public const string Finished = "Finished";

    /// <summary>所有产类 Key 的有序列表</summary>
    public static readonly string[] All =
    [
        RoughTube, InProgress, Finished
    ];

    /// <summary>Key → 规范中文（显示兜底）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RoughTube] = "荒管",
            [InProgress] = "在制",
            [Finished] = "成品",
        };

    /// <summary>规范中文 → Key（迁移前存量归一用）</summary>
    public static readonly IReadOnlyDictionary<string, string> ChineseToKey =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["荒管"] = RoughTube,
            ["在制"] = InProgress,
            ["成品"] = Finished,
        };

    private static readonly HashSet<string> KeySet = new(All, StringComparer.Ordinal);

    /// <summary>是否为成品</summary>
    public static bool IsFinished(string? value)
        => value == Finished;

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
