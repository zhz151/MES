// 文件路径: MES.Core/DTOs/OrderWorkOrderStatusDto.cs

using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Order;

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
    /// 交期起始（项次最小交货日期）
    /// </summary>
    public DateTime? DeliveryStart { get; set; }

    /// <summary>
    /// 交期截止（项次最大交货日期）
    /// </summary>
    public DateTime? DeliveryEnd { get; set; }

    /// <summary>
    /// 延期罚款（项次中任意一个是则标为是）
    /// </summary>
    public bool HasDelayPenalty { get; set; }

    /// <summary>
    /// 订单总重量（合同重量汇总，取整）
    /// </summary>
    public int TotalContractWeight { get; set; }

    /// <summary>
    /// 含项次数
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// 现工单数
    /// </summary>
    public int WorkOrderCount { get; set; }

    /// <summary>
    /// 工单状态
    /// </summary>
    public WorkOrderStatus WorkOrderStatus { get; set; }

    /// <summary>
    /// 工单状态中文显示
    /// </summary>
    public string WorkOrderStatusDisplay => EnumHelper.GetDisplayName(WorkOrderStatus);

    /// <summary>
    /// 工单状态文本
    /// </summary>
    public string WorkOrderStatusText
    {
        get
        {
            return WorkOrderStatus switch
            {
                WorkOrderStatus.NotGenerated => "未编制",
                WorkOrderStatus.Pending => "待修正",
                WorkOrderStatus.Confirmed => "已确定",
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