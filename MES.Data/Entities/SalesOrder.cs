using MES.Core.Enums;

namespace MES.Data.Entities;

/// <summary>
/// 销售单实体
/// </summary>
public class SalesOrder : BaseEntity
{
    /// <summary>
    /// 订单号，唯一索引
    /// </summary>
    public string OrderNumber { get; set; } = null!;

    /// <summary>
    /// 签订日期
    /// </summary>
    public DateTime SignDate { get; set; }

    /// <summary>
    /// 客户ID，外键到CustomerProfile
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// 订单状态
    /// </summary>
    public SalesOrderStatus Status { get; set; }

    /// <summary>
    /// 乐观并发控制时间戳
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>
    /// 导航属性 - 客户档案
    /// </summary>
    public CustomerProfile Customer { get; set; } = null!;

    /// <summary>
    /// 导航属性 - 订单项次
    /// </summary>
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}