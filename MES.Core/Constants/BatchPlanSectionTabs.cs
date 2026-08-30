namespace MES.Core.Constants;

/// <summary>
/// 批次计划工段筛选 Tab 特殊常量（仅保留非配置驱动的固定项）。
/// Tab 主体已配置驱动（见 BatchPlanService.BuildSectionTabOptionsAsync）：
/// 冷轧冷拔类 = ProcessDefinitions 启用工序、普通工段 = StandardWorkDays 启用工段（扣除冷轧拔/检验/入库）、
/// 末尾固定「荒管检」「在制检」；「内抛+内修磨」已按启用工段拆分为「内抛」「内修磨」独立 Tab。
/// </summary>
public static class BatchPlanSectionTabs
{
    /// <summary>荒管检（荒管产类检验 Tab，固定项）</summary>
    public const string RoughTubeInspection = "荒管检";

    /// <summary>在制检（在制产类检验 Tab，固定项）</summary>
    public const string InProcessInspection = "在制检";
}
