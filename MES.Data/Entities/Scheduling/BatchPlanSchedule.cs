namespace MES.Data.Entities.Scheduling;

/// <summary>
/// 批次计划薄表 — 计划员手工编辑
/// 用于记录批次的流转计划、目标序、执行序、抢单标记和备注
/// </summary>
public class BatchPlanSchedule : BaseEntity
{
    /// <summary>批次ID（唯一，一个批次一条记录）</summary>
    public int BatchId { get; set; }

    /// <summary>流转</summary>
    public bool IsFlow { get; set; }

    /// <summary>等级</summary>
    public int FlowLevel { get; set; }

    /// <summary>流转目标</summary>
    public string? FlowTarget { get; set; }

    /// <summary>冷轧类型</summary>
    public string? FlowCRType { get; set; }

    /// <summary>外径跨度（持久化，可手动优化）</summary>
    public string? PlanOuterDiameterSpan { get; set; }

    /// <summary>执行规格</summary>
    public string? FlowExecSpec { get; set; }

    /// <summary>目标序（计划安排的快照值）</summary>
    public int? TargetSequence { get; set; }

    /// <summary>执行序（计划安排的快照值）</summary>
    public int? ExecutionSequence { get; set; }

    /// <summary>抢单</summary>
    public bool IsGrabOrder { get; set; }

    /// <summary>计划备注</summary>
    public string? PlanRemark { get; set; }
}
