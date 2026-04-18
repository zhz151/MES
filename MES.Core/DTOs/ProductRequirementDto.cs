// 文件路径: MES.Core/DTOs/ProductRequirementDto.cs
using MES.Core.Enums;

namespace MES.Core.DTOs;

/// <summary>
/// 产品要求 DTO
/// </summary>
public class ProductRequirementDto
{
    /// <summary>
    /// ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 订单项次ID
    /// </summary>
    public int OrderItemId { get; set; }

    /// <summary>
    /// 产品要求类型
    /// </summary>
    public RequirementType RequirementType { get; set; }

    /// <summary>
    /// 产品要求类型文本
    /// </summary>
    public string RequirementTypeText => RequirementType.ToString();

    /// <summary>
    /// 化学成分要求
    /// </summary>
    public string? ChemicalComposition { get; set; }

    /// <summary>
    /// 机械性能要求
    /// </summary>
    public string? MechanicalProperty { get; set; }

    /// <summary>
    /// 尺寸公差要求
    /// </summary>
    public string? ToleranceRequirement { get; set; }

    /// <summary>
    /// 表面质量要求
    /// </summary>
    public string? SurfaceQuality { get; set; }

    /// <summary>
    /// 无损检测要求
    /// </summary>
    public string? NdtRequirement { get; set; }

    /// <summary>
    /// 其他要求
    /// </summary>
    public string? OtherRequirement { get; set; }
}

/// <summary>
/// 创建/更新产品要求请求
/// </summary>
public class CreateProductRequirementRequest
{
    /// <summary>
    /// 产品要求类型
    /// </summary>
    public RequirementType RequirementType { get; set; } = RequirementType.Normal;

    /// <summary>
    /// 化学成分要求
    /// </summary>
    public string? ChemicalComposition { get; set; }

    /// <summary>
    /// 机械性能要求
    /// </summary>
    public string? MechanicalProperty { get; set; }

    /// <summary>
    /// 尺寸公差要求
    /// </summary>
    public string? ToleranceRequirement { get; set; }

    /// <summary>
    /// 表面质量要求
    /// </summary>
    public string? SurfaceQuality { get; set; }

    /// <summary>
    /// 无损检测要求
    /// </summary>
    public string? NdtRequirement { get; set; }

    /// <summary>
    /// 其他要求
    /// </summary>
    public string? OtherRequirement { get; set; }
}