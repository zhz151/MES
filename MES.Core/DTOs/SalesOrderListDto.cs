using MES.Core.Enums;

namespace MES.Core.DTOs;

/// <summary>
/// 订单列表 DTO
/// </summary>
public class SalesOrderListDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public DateTime SignDate { get; set; }
    public string CustomerName { get; set; } = null!;
    public string Salesman { get; set; } = null!;
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

    public SalesOrderStatus Status { get; set; }
    public string StatusText => Status.ToString();
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
}