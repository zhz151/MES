// 文件路径: MES.Core/DTOs/CreateProductionStandardRequest.cs

using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

/// <summary>
/// 创建产品标准请求
/// </summary>
public class CreateProductionStandardRequest
{
    /// <summary>
    /// 标准编码
    /// </summary>
    [Required(ErrorMessage = "标准编码不能为空")]
    [StringLength(50, ErrorMessage = "标准编码长度不能超过50")]
    public string StandardCode { get; set; } = string.Empty;

    /// <summary>
    /// 标准名称
    /// </summary>
    [Required(ErrorMessage = "标准名称不能为空")]
    [StringLength(100, ErrorMessage = "标准名称长度不能超过100")]
    public string StandardName { get; set; } = string.Empty;

    /// <summary>
    /// 备注
    /// </summary>
    [StringLength(500, ErrorMessage = "备注长度不能超过500")]
    public string? Remark { get; set; }

    /// <summary>
    /// 排序序号
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; set; } = true;
}