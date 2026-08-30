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

    /// <summary>
    /// 全量刷新 WorkOrderListSummary 读模型（遍历所有订单号逐单重建 + 清理读模型残留孤儿行）
    /// 供定时兜底任务使用，补齐增量刷新漏网的数据
    /// </summary>
    Task RefreshAllAsync();
}
