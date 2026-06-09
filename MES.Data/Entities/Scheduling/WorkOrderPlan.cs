namespace MES.Data.Entities.Scheduling;

/// <summary>
/// 工单计划薄表 — 手工覆盖字段
/// 计划员可在此覆盖系统计算的工单状态/紧急性/生产关注/流转性
/// null = 使用系统值（WorkOrderExecutionSummary），非 null = 手工覆盖
/// </summary>
public class WorkOrderPlan : BaseEntity
{
    /// <summary>工单ID（唯一，一个工单一条记录）</summary>
    public int WorkOrderId { get; set; }

    /// <summary>工单状态覆盖值（0=工单完成 1=原料锁定 2=生产执行 3=成品检验）</summary>
    public int? ScheduleStage { get; set; }

    /// <summary>紧急性覆盖值（A+急/A急/B顺/C缓/D缓）</summary>
    public string? UrgencyLevel { get; set; }

    /// <summary>生产关注工序覆盖值</summary>
    public string? ProductionAttentionProcess { get; set; }

    /// <summary>生产流转性覆盖值（正常/暂停/待料/疑问/略）</summary>
    public string? ProductionFlowProperty { get; set; }
}
