// 文件路径: MES.Core/DTOs/UpdateProductionStandardRequest.cs
namespace MES.Core.DTOs;

/// <summary>
/// 更新产品标准请求
/// </summary>
public class UpdateProductionStandardRequest
{
    /// <summary>
    /// 标准编码
    /// </summary>
    public string? StandardCode { get; set; }

    /// <summary>
    /// 标准名称
    /// </summary>
    public string? StandardName { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 排序序号（可空）
    /// </summary>
    public int? SortOrder { get; set; }

    /// <summary>
    /// 是否启用（可空）
    /// </summary>
    public bool? IsActive { get; set; }
}