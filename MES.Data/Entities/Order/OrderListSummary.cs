using MES.Core.Enums;

namespace MES.Data.Entities.Order;

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

    // ========== 工单执行聚合字段（从 WorkOrderExecutionSummary 聚合） ==========

    /// <summary>执行关注阶段（null=未排产, 0=完成, 1=原料锁定, 2=生产执行, 3=成品检验）</summary>
    public int? ScheduleStage { get; set; }

    /// <summary>紧急性（A+急/A急/B顺/C缓/D缓，取最紧急）</summary>
    public string? UrgencyLevel { get; set; }

    /// <summary>预计完成日期（取工单中最大预计完成日）</summary>
    public DateTime? EstimatedCompletionDate { get; set; }

    // ========== 业务完结 / 成品库存聚合字段（从 InventoryBatch + OutboundRecord 聚合） ==========

    /// <summary>成品入库量（订单成品入库重量聚合，仅 MaterialType=OrderFinished，订成-非交付态不计入）</summary>
    public decimal FinishedInboundWeight { get; set; }

    /// <summary>成品出库量（订单成品销售出库 SalesOut 出库重量聚合）</summary>
    public decimal FinishedOutboundWeight { get; set; }

    /// <summary>成品库存量（订单成品当前剩余重量聚合）</summary>
    public decimal FinishedStockWeight { get; set; }

    /// <summary>业务完结（主号完成 且 有成品入库 且 库存清零）</summary>
    public bool BusinessCompleted { get; set; }
}
