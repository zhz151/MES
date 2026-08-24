namespace MES.Core.Constants;

/// <summary>
/// 用料计划执行容差默认值常量（到料实投一致性判定）。
/// 对应 ConfigParameter 配置类目 MaterialPlanTolerance 的 InputConsistencyTolerance 键。
/// 三处消费（排序表达式/筛选表达式/DTO 计算属性）经 MaterialPlanToleranceProvider 静态快照读取——
/// API 启动 + ConfigParameterService 写操作后刷新快照（改配置表保存即生效），本常量仅作未接线/未配置时的兜底默认值。
/// </summary>
public static class MaterialPlanToleranceDefaults
{
    /// <summary>
    /// 到料实投一致性容差（±3%）：判定已投≈现可的一致档（±容差内）与疑问-超投/少投档位，
    /// 同时是阶段门控「错误-无需投料(5)」的缺口率阈值（理论缺失总料重 &gt; 计划投料总重 × 容差）。
    /// </summary>
    public const decimal InputConsistencyTolerance = 0.03m;

    /// <summary>疑问-到料超投判定系数（已投 &gt; 现可 × 1.03）</summary>
    public const decimal InputConsistencyUpper = 1m + InputConsistencyTolerance;

    /// <summary>投料滞后判定系数（已投 &lt; 现可 × 0.97）</summary>
    public const decimal InputConsistencyLower = 1m - InputConsistencyTolerance;
}
