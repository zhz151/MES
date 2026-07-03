namespace MES.Core.DTOs;

/// <summary>
/// 批次计划薄表 DTO — 对应 BatchPlanSchedule 实体
/// </summary>
public class BatchPlanScheduleDto
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public bool IsFlow { get; set; }
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
