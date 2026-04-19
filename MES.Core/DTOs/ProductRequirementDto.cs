// 文件路径: MES.Core/DTOs/ProductRequirementDto.cs
using MES.Core.Enums;

namespace MES.Core.DTOs;

/// <summary>
/// 产品要求 DTO
/// </summary>
public class ProductRequirementDto
{
    public int Id { get; set; }
    public int OrderItemId { get; set; }
    public RequirementType RequirementType { get; set; }
    public string RequirementTypeText => RequirementType.ToString();
    public string? ChemicalComposition { get; set; }
    public string? MechanicalProperty { get; set; }
    public string? ToleranceRequirement { get; set; }
    public string? SurfaceQuality { get; set; }
    public string? NdtRequirement { get; set; }
    public string? OtherRequirement { get; set; }
    
    /// <summary>
    /// 项次号（用于前端展示）
    /// </summary>
    public int Sequence { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedTime { get; set; }
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 创建/更新产品要求请求
/// </summary>
public class CreateProductRequirementRequest
{
    public RequirementType RequirementType { get; set; } = RequirementType.Normal;
    public string? ChemicalComposition { get; set; }
    public string? MechanicalProperty { get; set; }
    public string? ToleranceRequirement { get; set; }
    public string? SurfaceQuality { get; set; }
    public string? NdtRequirement { get; set; }
    public string? OtherRequirement { get; set; }
}