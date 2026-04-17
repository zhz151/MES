// 文件路径: MES.Core/DTOs/CustomerProfileDto.cs
using MES.Core.Enums;
using System.Text.Json.Serialization;

namespace MES.Core.DTOs;

/// <summary>
/// 客户档案 DTO
/// </summary>
public class CustomerProfileDto
{
    /// <summary>
    /// 客户ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 客户编码
    /// </summary>
    public string CustomerCode { get; set; } = string.Empty;

    /// <summary>
    /// 业务员
    /// </summary>
    public string Salesman { get; set; } = string.Empty;

    /// <summary>
    /// 客户单位
    /// </summary>
    public string CustomerUnit { get; set; } = string.Empty;

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


    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CustomerStatus Status { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}