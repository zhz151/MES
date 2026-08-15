namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 批次计划薄表 DTO — 对应 BatchPlanSchedule 实体
/// </summary>
public class BatchPlanScheduleDto
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    /// <summary>暂停（控制开关）：=是 时读时覆盖为非流转（保存时保留原流转字段），切回"否"自动恢复</summary>
    public bool IsPaused { get; set; }
    public bool IsFlow { get; set; }
    /// <summary>等级（1=特急 2=急 3=一般 4=略）</summary>
    public int FlowLevel { get; set; }
    public string? FlowTarget { get; set; }
    public string? FlowCRType { get; set; }
    public string? PlanOuterDiameterSpan { get; set; }
    public string? FlowExecSpec { get; set; }
    public int? TargetSequence { get; set; }
    public int? ExecutionSequence { get; set; }
    public bool IsGrabOrder { get; set; }
    public string? PlanRemark { get; set; }
}
