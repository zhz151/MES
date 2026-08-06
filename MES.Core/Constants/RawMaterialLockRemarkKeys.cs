namespace MES.Core.Constants;

/// <summary>
/// 原锁备注（RawMaterialLockRemark）四类英文稳定 Key 常量及双向映射。
/// 存储层与后端匹配一律使用英文 Key（QualityReplenish/ExecuteRework/ExecutePlan/ImprovePlan），
/// 显示层使用中文（A质量补料/B执行返整/C执行计划/D完善计划）。由 WorkOrderExecutionService
/// 在 ScheduleStage=2（原料锁定）主号级判定产生，属固定四值状态机（枚举化，非配置字典）。
/// 存量中文值经 ToKey/ToChinese 幂等兼容（2026-08-06 英文 Key 化后由迁移统一转英文）。
/// </summary>
public static class RawMaterialLockRemarkKeys
{
    /// <summary>A质量补料：投料不满足且附返整不满足，连返整量算上仍不足，真缺料需补料</summary>
    public const string QualityReplenish = "QualityReplenish";

    /// <summary>B执行返整：投料满足且附返整满足，缺口可由返整量补齐、处于返整执行</summary>
    public const string ExecuteRework = "ExecuteRework";

    /// <summary>C执行计划：投料不满足但计划状态满足/超量，需执行计划投料</summary>
    public const string ExecutePlan = "ExecutePlan";

    /// <summary>D完善计划：投料不满足且计划不完善，需完善计划</summary>
    public const string ImprovePlan = "ImprovePlan";

    /// <summary>全部四类 Key 有序列表</summary>
    public static readonly string[] All =
    [
        QualityReplenish, ExecuteRework, ExecutePlan, ImprovePlan
    ];

    /// <summary>Key → 规范中文（显示兜底）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [QualityReplenish] = "A质量补料",
            [ExecuteRework] = "B执行返整",
            [ExecutePlan] = "C执行计划",
            [ImprovePlan] = "D完善计划",
        };

    /// <summary>规范中文 → Key（迁移前存量归一用）</summary>
    public static readonly IReadOnlyDictionary<string, string> ChineseToKey =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["A质量补料"] = QualityReplenish,
            ["B执行返整"] = ExecuteRework,
            ["C执行计划"] = ExecutePlan,
            ["D完善计划"] = ImprovePlan,
        };

    private static readonly HashSet<string> KeySet = new(All, StringComparer.Ordinal);

    /// <summary>是否为合法四类 Key（Ordinal）</summary>
    public static bool IsKey(string? value)
        => !string.IsNullOrEmpty(value) && KeySet.Contains(value!);

    /// <summary>归一为稳定 Key：已是 Key 原样返回；中文反查；未知返回 null</summary>
    public static string? ToKey(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (KeySet.Contains(value)) return value;
        return ChineseToKey.TryGetValue(value, out var key) ? key : null;
    }

    /// <summary>归一为显示中文：Key → 中文；已是中文（迁移前存量）原样返回；未知返回 null</summary>
    public static string? ToChinese(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (KeyToChinese.TryGetValue(value, out var cn)) return cn;
        return value;
    }
}
