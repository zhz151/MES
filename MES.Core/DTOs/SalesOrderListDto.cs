// 文件路径: MES.Core/DTOs/SalesOrderListDto.cs
using MES.Core.Enums;

namespace MES.Core.DTOs;

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
    /// 订单状态（枚举值，用于逻辑判断）
    /// </summary>
    public SalesOrderStatus Status { get; set; }

    /// <summary>
    /// 订单状态文本（用于前端显示）
    /// </summary>
    public string StatusText => Status.ToString();

    /// <summary>
    /// 乐观并发控制版本号（列表页可能不需要，但保留以保持一致性）
    /// </summary>
    public byte[]? RowVersion { get; set; }
}