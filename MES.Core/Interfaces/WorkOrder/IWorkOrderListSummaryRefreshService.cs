namespace MES.Core.Interfaces.WorkOrder;

/// <summary>
/// 用料计划总览读模型刷新服务
/// </summary>
public interface IWorkOrderListSummaryRefreshService
{
    /// <summary>
    /// 按订单号刷新 WorkOrderListSummary 读模型
    /// </summary>
    Task RefreshBySalesOrderAsync(string salesOrderNo);
}
