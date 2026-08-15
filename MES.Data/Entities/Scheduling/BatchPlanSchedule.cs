namespace MES.Data.Entities.Scheduling;

/// <summary>
/// 批次计划薄表 — 计划员手工编辑
/// 用于记录批次的流转计划、目标序、执行序、抢单标记和备注
/// </summary>
public class BatchPlanSchedule : BaseEntity
{
    /// <summary>批次ID（唯一，一个批次一条记录）</summary>
    public int BatchId { get; set; }

    /// <summary>暂停（控制开关）：=是 时读时覆盖为"非流转"（流转/等级/流转位等字段按非流转显示），DB 保留原流转数据，切回"否"自动恢复</summary>
    public bool IsPaused { get; set; }

    /// <summary>流转</summary>
    public bool IsFlow { get; set; }

    /// <summary>等级（V5.28 五档：1=急+ 2=急 3=急- 4=一般 5=略，由冷轧排程/工单计划重算生成，特急A/B 手工档已删除）</summary>
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
