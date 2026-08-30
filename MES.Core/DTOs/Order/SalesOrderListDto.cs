using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Order;

/// <summary>
/// 订单列表 DTO
/// </summary>
public class SalesOrderListDto
{
    /// <summary>
    /// 订单ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 订单号
    /// </summary>
    public string OrderNumber { get; set; } = null!;

    /// <summary>
    /// 签订日期
    /// </summary>
    public DateTime SignDate { get; set; }

    /// <summary>
    /// 客户名称
    /// </summary>
    public string CustomerName { get; set; } = null!;

    /// <summary>
    /// 业务员
    /// </summary>
    public string Salesman { get; set; } = null!;

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
    /// 订单状态
    /// </summary>
    public SalesOrderStatus Status { get; set; }

    /// <summary>
    /// 乐观并发控制版本号
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>
    /// 订单下是否存在技术要求（任何项次有产品要求）
    /// </summary>
    public bool HasTechnicalRequirement { get; set; }

    /// <summary>
    /// 订单下第一个项次的ID（用于跳转编辑技术要求）
    /// </summary>
    public int? FirstOrderItemId { get; set; }

    /// <summary>
    /// 最后变更日期（项次最后一次更新时间）
    /// </summary>
    public DateTime? LastChangeDate { get; set; }

    /// <summary>
    /// 创建人（取自源头 SalesOrder，非读模型）
    /// </summary>
    public string CreatedBy { get; set; } = "";

    /// <summary>
    /// 创建时间（取自源头 SalesOrder，非读模型）
    /// </summary>
    public DateTimeOffset? CreatedTime { get; set; }

    /// <summary>
    /// 更新人（取自源头 SalesOrder，非读模型）
    /// </summary>
    public string UpdatedBy { get; set; } = "";

    // ========== 工单执行聚合字段 ==========

    /// <summary>
    /// 执行关注阶段（null=未排产, 0=主号暂停, 1=主号完成, 2=原料锁定, 3=生产执行, 4=成品检验）
    /// </summary>
    public int? ScheduleStage { get; set; }

    /// <summary>
    /// 执行关注阶段中文显示
    /// </summary>
    public string ScheduleStageText => IntStatusDisplayHelper.GetScheduleStageText(ScheduleStage, "未排产");

    /// <summary>
    /// 紧急性（A+急/A急/B顺/C缓/D缓，取最紧急）
    /// </summary>
    public string? UrgencyLevel { get; set; }

    /// <summary>
    /// 预计完成日期（取工单中最大预计完成日）
    /// </summary>
    public DateTime? EstimatedCompletionDate { get; set; }

    // ========== 业务完结 / 成品库存聚合字段 ==========

    /// <summary>
    /// 成品入库量（订单成品入库重量聚合，仅 MaterialType=OrderFinished，订成-非交付态不计入）
    /// </summary>
    public decimal FinishedInboundWeight { get; set; }

    /// <summary>
    /// 成品出库量（订单成品销售出库 SalesOut 出库重量聚合）
    /// </summary>
    public decimal FinishedOutboundWeight { get; set; }

    /// <summary>
    /// 成品库存量（订单成品当前剩余重量聚合）
    /// </summary>
    public decimal FinishedStockWeight { get; set; }

    /// <summary>
    /// 业务完结（主号完成 且 有成品入库 且 库存清零）
    /// </summary>
    public bool BusinessCompleted { get; set; }

    /// <summary>
    /// 业务完结中文显示
    /// </summary>
    public string BusinessCompletedText => BusinessCompleted ? "完结" : "否";
}