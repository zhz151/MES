namespace MES.Core.Interfaces;

/// <summary>
/// 用料计划总览读模型服务接口
/// </summary>
public interface IWorkOrderListSummaryService
{
    /// <summary>全量刷新所有工单的用料计划读模型</summary>
    Task RefreshAllAsync();

    /// <summary>刷新指定工单的用料计划读模型</summary>
    Task RefreshByWorkOrderAsync(int workOrderId);

    /// <summary>刷新指定销售单号的用料计划读模型</summary>
    Task RefreshBySalesOrderAsync(string salesOrderNo);

    /// <summary>刷新指定客户的用料计划读模型</summary>
    Task RefreshByCustomerAsync(int customerId);
}
