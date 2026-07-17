using MES.Core.Enums;
namespace MES.Core.DTOs.Materials;

public class MaterialDto
{
    public int Id { get; set; }
    public string MaterialCode { get; set; } = null!;
    public MaterialType MaterialCategory { get; set; }
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public bool IsActive { get; set; }
    public string? Remark { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public string CreatedBy { get; set; } = null!;
}

public class CreateMaterialRequest
{
    public MaterialType MaterialCategory { get; set; }
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public string? Remark { get; set; }
}

public class UpdateMaterialRequest
{
    public MaterialType? MaterialCategory { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public bool? IsActive { get; set; }
    public string? Remark { get; set; }
}
