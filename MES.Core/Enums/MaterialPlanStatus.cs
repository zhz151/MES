namespace MES.Core.Enums;

/// <summary>
/// 用料计划状态（4档，已取消"理论满足"并入"满足"）
/// </summary>
public enum MaterialPlanStatus
{
    /// <summary>
    /// 未计划 - 没有任何明细的用料信息
    /// </summary>
    NotPlanned = 0,

    /// <summary>
    /// 部分 - 有部分计划，但总量不够（工单级低于100%；主号级低于102%/105%）
    /// </summary>
    Partial = 1,

    /// <summary>
    /// 满足 - 总量在合理范围内（含原"理论满足"区间）
    /// </summary>
    Satisfied = 2,

    /// <summary>
    /// 超量 - 超过了设定范围上限
    /// </summary>
    Excess = 3
}
