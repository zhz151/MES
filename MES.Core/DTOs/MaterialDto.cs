namespace MES.Core.DTOs;

public class MaterialDto
{
    public int Id { get; set; }
    public string MaterialCategory { get; set; } = null!;
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public bool IsActive { get; set; }
    public string? Remark { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public string CreatedBy { get; set; } = null!;
}

public class CreateMaterialRequest
{
    public string MaterialCategory { get; set; } = null!;
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public string? Remark { get; set; }
}

public class UpdateMaterialRequest
{
    public string? MaterialCategory { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public bool? IsActive { get; set; }
    public string? Remark { get; set; }
}
