namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 保存工单计划薄表覆盖值请求
/// 全部字段可 null，null = 清除覆盖（使用系统值）
/// </summary>
public class SaveWorkOrderPlanRequest
{
    /// <summary>工单ID</summary>
    public int WorkOrderId { get; set; }

    /// <summary>工单状态覆盖（0=主号完成 1=原料锁定 2=生产执行 3=成品检验）</summary>
    public int? ScheduleStage { get; set; }

    /// <summary>紧急性覆盖</summary>
    public string? UrgencyLevel { get; set; }

    /// <summary>生产关注工序覆盖</summary>
    public string? ProductionAttentionProcess { get; set; }

    /// <summary>生产流转性覆盖</summary>
    public string? ProductionFlowProperty { get; set; }
}
