namespace MES.Core.Constants;

/// <summary>
/// 责任类型英文稳定 Key 常量及双向映射。存储层与后端匹配一律使用英文 Key
/// （如 "FactoryDepartment"），显示层使用中文（厂部/外购，可经配置表 LiabilityTypeDefinition 改名）。
/// 属可扩展配置字典：用户在配置表可新增责任类型（Key 固定、Name 可改）。
/// 代码分支匹配（如 MaterialPlanService 中"厂部"判断）一律用 Key。
/// </summary>
public static class LiabilityTypeKeys
{
    // ========== 内置责任类型英文 Key 常量 ==========
    /// <summary>厂部</summary>
    public const string FactoryDepartment = "FactoryDepartment";

    /// <summary>外购</summary>
    public const string OutsourcedPurchase = "OutsourcedPurchase";

    /// <summary>所有内置责任类型 Key 的有序列表</summary>
    public static readonly string[] All =
    [
        FactoryDepartment, OutsourcedPurchase
    ];

    /// <summary>Key → 规范中文（显示兜底）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [FactoryDepartment] = "厂部",
            [OutsourcedPurchase] = "外购",
        };

    /// <summary>规范中文 → Key（迁移前存量归一用）</summary>
    public static readonly IReadOnlyDictionary<string, string> ChineseToKey =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["厂部"] = FactoryDepartment,
            ["外购"] = OutsourcedPurchase,
        };

    private static readonly HashSet<string> KeySet = new(All, StringComparer.Ordinal);

    /// <summary>是否为合法责任类型 Key（Ordinal）</summary>
    public static bool IsKey(string? value)
        => !string.IsNullOrEmpty(value) && KeySet.Contains(value!);

    /// <summary>是否为厂部（代码分支判断专用）</summary>
    public static bool IsFactoryDepartment(string? value)
        => value == FactoryDepartment;

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
