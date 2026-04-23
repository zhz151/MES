namespace MES.Core.DTOs;

/// <summary>
/// 订单已取消-工单待删除 DTO
/// </summary>
public class CancelledOrderDto
{
    /// <summary>
    /// 订单ID
    /// </summary>
    public int SalesOrderId { get; set; }

    /// <summary>
    /// 订单号
    /// </summary>
    public string OrderNumber { get; set; } = null!;

    /// <summary>
    /// 签订日期
    /// </summary>
    public DateTime SignDate { get; set; }

    /// <summary>
    /// 业务员
    /// </summary>
    public string Salesman { get; set; } = null!;

    /// <summary>
    /// 客户名称
    /// </summary>
    public string CustomerName { get; set; } = null!;

    /// <summary>
    /// 关联的工单ID
    /// </summary>
    public int WorkOrderId { get; set; }

    /// <summary>
    /// 工单号
    /// </summary>
    public string WorkOrderNo { get; set; } = null!;
}