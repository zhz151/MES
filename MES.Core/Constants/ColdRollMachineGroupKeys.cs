namespace MES.Core.Constants;

/// <summary>
/// 冷轧排程机台类型组常量：稳定英文 Key（比较用）+ 显示名 + ProcessType 归并集合 + 供给目标组 SupplyTargetGroupKey（可空）。
/// 2026-08-29 起本常量**仅作种子/测试的规范定义源**，引擎归组已配置表驱动
/// （ColdRollMachineGroupConfigs，见 <see cref="MES.Data.Entities.Scheduling.ColdRollMachineGroupConfig"/>），
/// 排程建议/排机估算运行时从配置表加载（ColdRollPlanService.LoadMachineGroupsAsync）。
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
    /// 机台类型组定义数组：Key（稳定标识）/ Display（显示名）/ Keys（ProcessType 归并集合）/ SupplyTargetGroupKey（供给目标组 Key，可空）。
    /// 仅作种子预置（DbInitializer）与测试内存库组种子的规范定义源；
    /// 引擎运行时不读本数组，改读配置表（ColdRollPlanService.LoadMachineGroupsAsync）。
    /// 供需链由 SupplyTargetGroupKey 显式表达（2026-08-29 方案 A，组角色字段已移除）：5060 → "2030"，允许多条并行链/多级链。
    /// </summary>
    public static readonly (string Key, string Display, string[] Keys, string? SupplyTargetGroupKey)[] Groups =
    [
        (Roll5060, Roll5060Display, new[] { ProcessKeys.ColdRoll50, ProcessKeys.ColdRoll60 }, Roll2030),
        (Roll2030, Roll2030Display, new[] { ProcessKeys.ColdRoll20, ProcessKeys.ColdRoll30 }, null),
        (ThreeRoll, ThreeRollDisplay, new[] { ProcessKeys.ThreeRollColdRoll }, null),
        (Draw, DrawDisplay, new[] { ProcessKeys.ColdDraw }, null),
    ];
}
