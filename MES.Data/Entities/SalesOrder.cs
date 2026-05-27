using MES.Core.Enums;

namespace MES.Data.Entities;

/// <summary>
/// 销售订单实体
/// </summary>
public class SalesOrder : BaseEntity
{
    /// <summary>
    /// 订单号（业务唯一标识）
    /// </summary>
    public string OrderNumber { get; set; } = null!;

    /// <summary>
    /// 签订日期
    /// </summary>
    public DateTime SignDate { get; set; }

    /// <summary>
    /// 客户ID（外键）
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// 订单状态
    /// </summary>
    public SalesOrderStatus Status { get; set; }

    /// <summary>
    /// 乐观并发控制版本号
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>
    /// 最后项次变更时间
    /// </summary>
    public DateTimeOffset? LastItemChangeTime { get; set; }

    /// <summary>
    /// 客户信息
    /// </summary>
    public CustomerProfile Customer { get; set; } = null!;

    /// <summary>
    /// 订单项次列表
    /// </summary>
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
