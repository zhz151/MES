namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 工单执行看板聚合项（**按执行阶段一行**）。
/// ⚠️ 单数一律按**订单号**（SalesOrderNo）去重统计（2026-09-10 起；此前为按工单号计），
/// 故同一订单下的多张工单只算 1 单。
/// </summary>
public class WorkOrderExecutionDashboardItem
{
    /// <summary>执行阶段 (1=原料锁定 2=生产执行 3=成品检验)</summary>
    public int ScheduleStage { get; set; }

    /// <summary>该阶段订单数（按订单号去重）</summary>
    public int OrderCount { get; set; }

    /// <summary>该阶段吨位汇总</summary>
    public decimal TotalWeight { get; set; }

    /// <summary>该阶段急单订单数（A+急/A急，按订单号去重）</summary>
    public int UrgentOrderCount { get; set; }

    /// <summary>该阶段急单吨位汇总</summary>
    public decimal UrgentWeight { get; set; }
}
