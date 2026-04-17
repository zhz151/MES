using System.ComponentModel.DataAnnotations;
using MES.Core.Enums; 

namespace MES.Core.DTOs;

/// <summary>
/// 更新客户请求
/// </summary>
public class UpdateCustomerRequest
{
    /// <summary>
    /// 客户编码
    /// </summary>
    public string? CustomerCode { get; set; }

    /// <summary>
    /// 业务员
    /// </summary>
    public string? Salesman { get; set; }

    /// <summary>
    /// 客户单位
    /// </summary>
    public string? CustomerUnit { get; set; }

    /// <summary>
    /// 最终用户
    /// </summary>
    public string? EndCustomer { get; set; }

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
    public CustomerStatus? Status { get; set; } 

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}