namespace MES.Core.Constants;

/// <summary>
/// 生产概览各工段日产能初始化默认值（吨/天），**仅供 DbInitializer 种子初始化新库**，
/// 生产服务（ProductionOverviewService）不引用、无运行时兜底（2026-08-30 用户决策：运行时产能 100% 来自配置表
/// DailyProductionCapacities，未配置组产能=0 → 预计天数空，需在日产能配置页补录）。
/// 冷轧/冷拔机台组默认产能按组 GroupKey 索引（机台组由配置表动态驱动）。
/// </summary>
public static class ProductionOverviewDefaults
{
    /// <summary>荒管抛光日产能初始化默认(吨/天)，仅种子用</summary>
    public const decimal Polish = 12m;

    /// <summary>机台组日产能初始化默认值（吨/天），键=冷轧机台组 GroupKey，仅种子用。</summary>
    public static readonly IReadOnlyDictionary<string, decimal> ForGroup =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["110"] = 3m,
            ["5060"] = 11m,
            ["2030"] = 9m,
            ["ThreeRoll"] = 0.5m,
            ["Draw"] = 3m,
        };
}
