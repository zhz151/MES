namespace MES.Core.Constants;

/// <summary>
/// 冷轧排程机台类型组常量：稳定英文 Key（比较用）+ 显示名 + ProcessType 归并集合。
/// 消除排程建议/排机估算两处重复的 machineTypeGroups 元组定义与中文显示名比较键。
/// </summary>
public static class ColdRollMachineGroupKeys
{
    // ========== 机台类型组英文稳定 Key（逻辑比较用，禁止用显示名做匹配键） ==========
    /// <summary>冷轧5060（50/60 轧机）</summary>
    public const string Roll5060 = "5060";

    /// <summary>冷轧2030（20/30 轧机）</summary>
    public const string Roll2030 = "2030";

    /// <summary>冷轧三辊</summary>
    public const string ThreeRoll = "ThreeRoll";

    /// <summary>冷拔</summary>
    public const string Draw = "Draw";

    // ========== 机台类型组显示名（前端展示） ==========
    public const string Roll5060Display = "冷轧5060";
    public const string Roll2030Display = "冷轧2030";
    public const string ThreeRollDisplay = "冷轧三辊";
    public const string DrawDisplay = "冷拔";

    /// <summary>
    /// 机台类型组定义数组：Key（稳定标识）/ Display（显示名）/ Keys（ProcessType 归并集合）。
    /// 排程建议 BuildScheduleSuggestionCoreAsync 与排机估算 GetMachineEstimateCoreAsync 共用。
    /// </summary>
    public static readonly (string Key, string Display, string[] Keys)[] Groups =
    [
        (Roll5060, Roll5060Display, new[] { ProcessKeys.ColdRoll50, ProcessKeys.ColdRoll60 }),
        (Roll2030, Roll2030Display, new[] { ProcessKeys.ColdRoll20, ProcessKeys.ColdRoll30 }),
        (ThreeRoll, ThreeRollDisplay, new[] { ProcessKeys.ThreeRollColdRoll }),
        (Draw, DrawDisplay, new[] { ProcessKeys.ColdDraw }),
    ];
}
