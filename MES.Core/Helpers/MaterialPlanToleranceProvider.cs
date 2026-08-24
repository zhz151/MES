using MES.Core.Constants;

namespace MES.Core.Helpers;

/// <summary>
/// 到料实投一致性容差静态快照（±3%，ConfigParameter.MaterialPlanTolerance.InputConsistencyTolerance 键）。
/// 三处消费（排序表达式 G3PlanInputConsistencyExpr / 筛选 ApplyComputedFilters / DTO 计算属性 PlanInputConsistency）
/// 读本快照统一值，保证筛选、排序与列表显示档位口径一致。
/// 由 API 启动 + ConfigParameterService 写操作（MaterialPlanTolerance 类目保存/删除）后注入，改配置表保存即生效。
/// 未接线/未配置时回退 MaterialPlanToleranceDefaults 常量默认值。
/// </summary>
public static class MaterialPlanToleranceProvider
{
    /// <summary>到料实投一致性容差（±3%，档5缺口率阈值同用）</summary>
    public static decimal InputConsistencyTolerance { get; set; } = MaterialPlanToleranceDefaults.InputConsistencyTolerance;

    /// <summary>疑问-到料超投判定系数（已投 &gt; 现可 × 上界）</summary>
    public static decimal InputConsistencyUpper => 1m + InputConsistencyTolerance;

    /// <summary>投料滞后判定系数（已投 &lt; 现可 × 下界）</summary>
    public static decimal InputConsistencyLower => 1m - InputConsistencyTolerance;

    /// <summary>应用配置值（无值保持默认），由 API 启动 + ConfigParameter 写操作后调用</summary>
    public static void Apply(decimal? tolerance)
    {
        if (tolerance.HasValue)
            InputConsistencyTolerance = tolerance.Value;
    }
}
