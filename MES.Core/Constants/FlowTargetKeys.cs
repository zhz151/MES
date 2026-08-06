namespace MES.Core.Constants;

/// <summary>
/// 冷轧排程流转目标英文稳定 Key 常量及双向映射。存储层与后端匹配一律使用英文 Key（如 "CompletionColdRoll"），
/// 显示层使用中文（成检/完工冷轧/冷轧）。由 BatchPlanDto.FlowTarget 按 FlowTrigger 计算产生，
/// 并落库到 BatchPlanSchedule.FlowTarget（可前端覆盖），属固定三值状态机（枚举化，非配置字典）。
/// </summary>
public static class FlowTargetKeys
{
    // ========== 3 个流转目标英文 Key 常量 ==========
    /// <summary>成检（关注工序触发）</summary>
    public const string Inspection = "Inspection";

    /// <summary>完工冷轧（完工类型触发）</summary>
    public const string CompletionColdRoll = "CompletionColdRoll";

    /// <summary>冷轧（轧制类型触发）</summary>
    public const string ColdRoll = "ColdRoll";

    /// <summary>Key → 规范中文（显示兜底）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Inspection] = "成检",
            [CompletionColdRoll] = "完工冷轧",
            [ColdRoll] = "冷轧",
        };

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
