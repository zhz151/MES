namespace MES.Core.Constants;

/// <summary>
/// 流转目标英文稳定 Key 常量及双向映射。存储层与后端匹配一律使用英文 Key（如 "CompletionColdRoll"），
/// 显示层使用中文。规则1 冷轧排程命中时由 BatchPlanDto.FlowTarget 按 FlowTrigger 计算产生（成检/完工冷轧/冷轧，
/// 三值状态机），规则2 重点生产批次按冷轧类型补充档位（荒管检/在制检/成品检验/冷轧），
/// 并落库到 BatchPlanSchedule.FlowTarget（可前端覆盖），属固定六值状态机（枚举化，非配置字典）。
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

    /// <summary>荒管检（规则2 按冷轧类型=荒管处理补充的档位）</summary>
    public const string RoughTubeCheck = "RoughTubeCheck";

    /// <summary>在制检（规则2 按冷轧类型=在制修检补充的档位）</summary>
    public const string InProcessCheck = "InProcessCheck";

    /// <summary>成品检验（规则2 按冷轧类型=生产收尾补充的档位）</summary>
    public const string FinalCheck = "FinalCheck";

    /// <summary>Key → 规范中文（显示兜底）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Inspection] = "成检",
            [CompletionColdRoll] = "完工冷轧",
            [ColdRoll] = "冷轧",
            [RoughTubeCheck] = "荒管检",
            [InProcessCheck] = "在制检",
            [FinalCheck] = "成品检验",
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
