using MES.Core.Enums;

namespace MES.Data.Entities;

public class ProductRequirement : BaseEntity
{
    public int OrderItemId { get; set; }

    public int? StandardId { get; set; }

    public RequirementType RequirementType { get; set; }

    public string? ChemicalComposition { get; set; }
    public string? MechanicalProperty { get; set; }

    public string? ToleranceRequirement { get; set; }

    public string? SurfaceQuality { get; set; }

    public string? NdtRequirement { get; set; }
    public string? OtherRequirement { get; set; }

    public OrderItem OrderItem { get; set; } = null!;

    public ProductionStandard? Standard { get; set; }
}