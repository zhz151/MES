// 文件路径: MES.Core/DTOs/CreateCustomerRequest.cs
using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

/// <summary>
/// 创建客户请求
/// </summary>
public class CreateCustomerRequest
{
    /// <summary>
    /// 客户编码
    /// </summary>
    [Required(ErrorMessage = "客户编码不能为空")]
    [StringLength(50, ErrorMessage = "客户编码长度不能超过50")]
    public string CustomerCode { get; set; } = string.Empty;

    /// <summary>
    /// 业务员
    /// </summary>
    [Required(ErrorMessage = "业务员不能为空")]
    [StringLength(50, ErrorMessage = "业务员名称长度不能超过50")]
    public string Salesman { get; set; } = string.Empty;

    /// <summary>
    /// 客户单位
    /// </summary>
    [Required(ErrorMessage = "客户单位不能为空")]
    [StringLength(200, ErrorMessage = "客户单位长度不能超过200")]
    public string CustomerUnit { get; set; } = string.Empty;

    /// <summary>
    /// 最终用户
    /// </summary>
    [StringLength(200, ErrorMessage = "最终用户长度不能超过200")]
    public string? EndCustomer { get; set; }

    /// <summary>
    /// 联系人
    /// </summary>
    [StringLength(50, ErrorMessage = "联系人长度不能超过50")]
    public string? ContactPerson { get; set; }

    /// <summary>
    /// 联系电话
    /// </summary>
    [StringLength(50, ErrorMessage = "联系电话长度不能超过50")]
    public string? ContactPhone { get; set; }

    /// <summary>
    /// 联系地址
    /// </summary>
    [StringLength(500, ErrorMessage = "联系地址长度不能超过500")]
    public string? Address { get; set; }

    /// <summary>
    /// 客户状态（默认Active）
    /// </summary>
    public string Status { get; set; } = "Active";

    /// <summary>
    /// 备注
    /// </summary>
    [StringLength(500, ErrorMessage = "备注长度不能超过500")]
    public string? Remark { get; set; }
}