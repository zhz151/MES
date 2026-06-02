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

    /// <summary>所有工序名称的有序列表（用于下拉选择）</summary>
    public static readonly string[] All =
    [
        RoughTubeProcessing, InProcessRepair, ColdRoll60, ColdRoll50,
        ColdRoll30, ColdRoll20, ThreeRollColdRoll, ColdDraw
    ];

    /// <summary>是否为冷轧系列（含三辊冷轧）</summary>
    public static bool IsColdRoll(string? processName) =>
        processName is ColdRoll60 or ColdRoll50 or ColdRoll30 or ColdRoll20 or ThreeRollColdRoll;

    /// <summary>是否为冷轧或冷拔（需要重量跟踪的工序）</summary>
    public static bool IsColdRollOrDraw(string? processName) =>
        IsColdRoll(processName) || processName == ColdDraw;
}
