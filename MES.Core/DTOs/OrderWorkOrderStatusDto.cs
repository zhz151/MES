// 文件路径: MES.Core/DTOs/OrderWorkOrderStatusDto.cs

namespace MES.Core.DTOs;

/// <summary>
/// 工单首页订单状态 DTO
/// </summary>
public class OrderWorkOrderStatusDto
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
    /// 最终客户
    /// </summary>
    public string? EndCustomer { get; set; }

    /// <summary>
    /// 工单状态（NotGenerated/Pending/Confirmed/Cancelled）
    /// </summary>
    public string WorkOrderStatus { get; set; } = null!;

    /// <summary>
    /// 工单状态文本
    /// </summary>
    public string WorkOrderStatusText
    {
        get
        {
            return WorkOrderStatus switch
            {
                "NotGenerated" => "未编制",
                "Pending" => "待修正",
                "Confirmed" => "已确定",
                "Cancelled" => "已取消",
                _ => "未知"
            };
        }
    }

    /// <summary>
    /// 是否存在工单
    /// </summary>
    public bool HasWorkOrder { get; set; }

    /// <summary>
    /// 工单ID（如有）
    /// </summary>
    public int? WorkOrderId { get; set; }
}