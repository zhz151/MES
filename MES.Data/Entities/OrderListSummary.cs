using MES.Core.Enums;

namespace MES.Data.Entities;

/// <summary>
/// 订单列表读模型（物化表，从 SalesOrders + OrderItems + CustomerProfiles + ProductRequirements 聚合计算）
/// 在订单/项次/客户/技术要求变更时自动刷新
/// </summary>
public class OrderListSummary : BaseEntity
{
    /// <summary>销售订单ID（唯一，一个订单一条记录）</summary>
    public int OrderId { get; set; }

    /// <summary>订单号</summary>
    public string OrderNumber { get; set; } = null!;

    /// <summary>签订日期</summary>
    public DateTime SignDate { get; set; }

    /// <summary>客户名称</summary>
    public string CustomerName { get; set; } = null!;

    /// <summary>业务员</summary>
    public string Salesman { get; set; } = null!;

    /// <summary>最终客户</summary>
    public string? EndCustomer { get; set; }

    /// <summary>交期起始（项次最小交货日期）</summary>
    public DateTime? DeliveryStart { get; set; }

    /// <summary>交期截止（项次最大交货日期）</summary>
    public DateTime? DeliveryEnd { get; set; }

    /// <summary>是否有延期罚款</summary>
    public bool HasDelayPenalty { get; set; }

    /// <summary>订单总重量（合同重量汇总，取整）</summary>
    public int TotalContractWeight { get; set; }

    /// <summary>含项次数</summary>
    public int ItemCount { get; set; }

    /// <summary>有技术要求的项次数</summary>
    public int HasTechReqCount { get; set; }

    /// <summary>订单状态</summary>
    public SalesOrderStatus Status { get; set; }

    /// <summary>乐观并发令牌</summary>
    public byte[]? RowVersion { get; set; }

    /// <summary>最后变更日期</summary>
    public DateTime? LastChangeDate { get; set; }

    /// <summary>第一个项次的ID（用于跳转技术要求）</summary>
    public int? FirstOrderItemId { get; set; }
}
