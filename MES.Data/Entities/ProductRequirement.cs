using MES.Core.Enums;

namespace MES.Data.Entities;

/// <summary>
/// 产品要求实体
/// </summary>
public class ProductRequirement : BaseEntity
{
    /// <summary>
    /// 订单项次ID，外键到OrderItem（唯一约束）
    /// </summary>
    public int OrderItemId { get; set; }

    /// <summary>
    /// 标准ID，外键到ProductionStandard
    /// </summary>
    public int? StandardId { get; set; }

    /// <summary>
    /// 产品要求类型
    /// </summary>
    public RequirementType RequirementType { get; set; }

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

    /// <summary>
    /// 导航属性 - 订单项次
    /// </summary>
    public OrderItem OrderItem { get; set; } = null!;

    /// <summary>
    /// 导航属性 - 产品标准
    /// </summary>
    public ProductionStandard? Standard { get; set; }
}