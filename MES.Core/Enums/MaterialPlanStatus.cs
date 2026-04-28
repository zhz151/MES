namespace MES.Core.Enums;

/// <summary>
/// 用料计划状态（5档）
/// </summary>
public enum MaterialPlanStatus
{
    /// <summary>
    /// 未计划 - 没有任何明细的用料信息
    /// </summary>
    NotPlanned = 0,

    /// <summary>
    /// 部分 - 有部分计划，但总量不够（低于100%）
    /// </summary>
    Partial = 1,

    /// <summary>
    /// 理论满足 - 满足率≥100%，但未达到原"满足"标准（用于有次号的工单）
    /// </summary>
    TheoreticalSatisfied = 2,

    /// <summary>
    /// 满足 - 总量在合理范围内
    /// </summary>
    Satisfied = 3,

    /// <summary>
    /// 超量 - 超过了设定范围上限
    /// </summary>
    Excess = 4
}
