// 文件路径: MES.Core/DTOs/ProductionStandardDto.cs

namespace MES.Core.DTOs;

/// <summary>
/// 产品标准 DTO
/// </summary>
public class ProductionStandardDto
{
    /// <summary>
    /// 主键
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 标准编码
    /// </summary>
    public string StandardCode { get; set; } = string.Empty;

    /// <summary>
    /// 标准名称
    /// </summary>
    public string StandardName { get; set; } = string.Empty;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 排序序号
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; set; }
}