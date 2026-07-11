namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 工单执行状况刷新结果
/// </summary>
public class WorkOrderExecutionRefreshResultDto
{
    /// <summary>总工单数</summary>
    public int TotalWorkOrders { get; set; }

    /// <summary>成功刷新数</summary>
    public int RefreshedCount { get; set; }
}
