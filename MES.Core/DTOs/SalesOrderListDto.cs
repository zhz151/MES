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
    public SalesOrderStatus Status { get; set; }
    public string StatusText => Status.ToString();
    public byte[]? RowVersion { get; set; }
    
    /// <summary>
    /// 订单下是否存在技术要求（任何项次有产品要求）
    /// </summary>
    public bool HasTechnicalRequirement { get; set; }
    
    /// <summary>
    /// 订单下第一个项次的ID（用于跳转编辑技术要求）
    /// </summary>
    public int? FirstOrderItemId { get; set; }
}