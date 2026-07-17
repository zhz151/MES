using MES.Core.Enums;

namespace MES.Data.Entities.Order;

/// <summary>
/// 客户档案实体
/// </summary>
public class CustomerProfile : BaseEntity
{
    /// <summary>
    /// 客户编码
    /// </summary>
    public string CustomerCode { get; set; } = null!;

    /// <summary>
    /// 业务员
    /// </summary>
    public string Salesman { get; set; } = null!;

    /// <summary>
    /// 客户单位
    /// </summary>
    public string CustomerUnit { get; set; } = null!;

    /// <summary>
    /// 最终用户
    /// </summary>
    public string EndCustomer { get; set; } = string.Empty;

    /// <summary>
    /// 联系人
    /// </summary>
    public string? ContactPerson { get; set; }

    /// <summary>
    /// 联系电话
    /// </summary>
    public string? ContactPhone { get; set; }

    /// <summary>
    /// 联系地址
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// 客户状态
    /// </summary>
    public CustomerStatus Status { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    // 销售订单导航已移除（订单仅通过快照字段引用客户，不维护 FK）
}
