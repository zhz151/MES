// 文件路径: MES.Core/DTOs/UpdateSalesOrderRequest.cs
using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

/// <summary>
/// 更新订单请求
/// </summary>
public class UpdateSalesOrderRequest
{
    /// <summary>
    /// 订单号
    /// </summary>
    [StringLength(11, MinimumLength = 11, ErrorMessage = "订单号必须为11位字符")]
    public string? OrderNumber { get; set; }

    /// <summary>
    /// 签订日期
    /// </summary>
    public DateTime? SignDate { get; set; }

    /// <summary>
    /// 客户ID
    /// </summary>
    public int? CustomerId { get; set; }

    /// <summary>
    /// 订单状态
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 乐观并发控制版本号
    /// </summary>
    [Required(ErrorMessage = "版本号不能为空")]
    public byte[] RowVersion { get; set; } = null!;
}