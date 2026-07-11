namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 工单执行看板聚合项（按 ScheduleStage × UrgencyLevel 分组）
/// </summary>
public class WorkOrderExecutionDashboardItem
{
    /// <summary>执行阶段 (1=原料锁定 2=生产执行 3=成品检验)</summary>
    public int ScheduleStage { get; set; }

    /// <summary>紧急程度 (A+急/A急/B顺/C缓/D缓/E停)</summary>
    public string? UrgencyLevel { get; set; }

    /// <summary>工单数</summary>
    public int OrderCount { get; set; }

    /// <summary>吨位汇总</summary>
    public decimal TotalWeight { get; set; }
}
