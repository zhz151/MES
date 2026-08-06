namespace MES.Core.Constants;

/// <summary>
/// 工序名称常量定义。所有涉及工序名称的代码必须引用此处定义的常量，禁止直接使用字符串字面量。
/// </summary>
public static class ProcessNames
{
    public const string RoughTubeProcessing = "荒管处理";
    public const string InProcessRepair = "在制修检";
    public const string ColdRoll60 = "60冷轧";
    public const string ColdRoll50 = "50冷轧";
    public const string ColdRoll30 = "30冷轧";
    public const string ColdRoll20 = "20冷轧";
    public const string ThreeRollColdRoll = "三辊冷轧";
    public const string ColdDraw = "冷拔";
    public const string AdditionalFinalInspection = "附加成检";

    /// <summary>所有工序名称的有序列表（用于下拉选择）</summary>
    public static readonly string[] All =
    [
        RoughTubeProcessing, InProcessRepair, ColdRoll60, ColdRoll50,
        ColdRoll30, ColdRoll20, ThreeRollColdRoll, ColdDraw,
        AdditionalFinalInspection
    ];

    // ========== ProcessKey 属性名 → 中文名映射 ==========
    /// <summary>key=ProcessKey 属性名, value=工序中文名</summary>
    public static readonly Dictionary<string, string> PropertyToName = new()
    {
        ["RoughTubeProcessing"] = RoughTubeProcessing,
        ["InProcessRepair"] = InProcessRepair,
        ["ColdRoll60"] = ColdRoll60,
        ["ColdRoll50"] = ColdRoll50,
        ["ColdRoll30"] = ColdRoll30,
        ["ColdRoll20"] = ColdRoll20,
        ["ThreeRollColdRoll"] = ThreeRollColdRoll,
        ["ColdDraw"] = ColdDraw,
        ["AdditionalFinalInspection"] = AdditionalFinalInspection,
    };

    // ========== 别名映射（数据导入/修复时匹配变体名称） ==========
    /// <summary>key=别名, value=标准工序名。当前存量均为规范中文无变体，预留结构供导入兼容。</summary>
    public static readonly Dictionary<string, string> Aliases = new();
}
