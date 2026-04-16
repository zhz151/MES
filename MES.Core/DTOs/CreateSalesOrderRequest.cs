using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

/// <summary>
/// 创建订单请求
/// </summary>
public class CreateSalesOrderRequest
{
    /// <summary>
    /// 订单号（11位字符）
    /// </summary>
    [Required(ErrorMessage = "订单号不能为空")]
    [StringLength(11, MinimumLength = 11, ErrorMessage = "订单号必须为11位字符")]
    public string OrderNumber { get; set; } = null!;

    /// <summary>
    /// 签订日期
    /// </summary>
    [Required(ErrorMessage = "签订日期不能为空")]
    public DateTime SignDate { get; set; }

    /// <summary>
    /// 客户ID
    /// </summary>
    [Required(ErrorMessage = "客户不能为空")]
    public int CustomerId { get; set; }

    /// <summary>
    /// 订单项次列表（至少1个）
    /// </summary>
    [Required(ErrorMessage = "订单项次不能为空")]
    [MinLength(1, ErrorMessage = "订单必须至少包含一个项次")]
    public List<CreateOrderItemRequest> Items { get; set; } = new List<CreateOrderItemRequest>();
}