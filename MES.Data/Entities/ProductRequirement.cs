using MES.Core.Enums;

namespace MES.Data.Entities;

/// <summary>
/// 产品要求实体（与订单项次一对一关系）
/// </summary>
public class ProductRequirement : BaseEntity
{
    /// <summary>
    /// 订单项次ID（外键）
    /// </summary>
    public int OrderItemId { get; set; }

    /// <summary>
    /// 技术要求类型
    /// </summary>
    public RequirementType RequirementType { get; set; }

    /// <summary>
    /// 化学成分要求
    /// </summary>
    public string? ChemicalComposition { get; set; }

    /// <summary>
    /// 力学性能要求
    /// </summary>
    public string? MechanicalProperty { get; set; }

    /// <summary>
    /// 公差要求
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
    /// 所属订单项次
    /// </summary>
    public OrderItem OrderItem { get; set; } = null!;
}
