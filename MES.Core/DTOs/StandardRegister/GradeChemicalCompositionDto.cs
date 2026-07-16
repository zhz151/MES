using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs.StandardRegister;

public class GradeChemicalCompositionDto
{
    public int Id { get; set; }
    public string StandardGrade { get; set; } = string.Empty;
    public string? StandardGradeCategory { get; set; }
    public string? Carbon { get; set; }
    public string? Silicon { get; set; }
    public string? Manganese { get; set; }
    public string? Phosphorus { get; set; }
    public string? Sulfur { get; set; }
    public string? Nickel { get; set; }
    public string? Chromium { get; set; }
    public string? Molybdenum { get; set; }
    public string? Copper { get; set; }
    public string? Nitrogen { get; set; }
    public string? Niobium { get; set; }
    public string? Titanium { get; set; }
    public string? Iron { get; set; }
    public string? Aluminum { get; set; }
    public string? Tungsten { get; set; }
}

public class CreateGradeChemicalCompositionRequest
{
    [Required(ErrorMessage = "标准牌号不能为空")]
    [StringLength(50, ErrorMessage = "标准牌号长度不能超过50")]
    public string StandardGrade { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "标准牌号类别长度不能超过50")]
    public string? StandardGradeCategory { get; set; }

    [StringLength(100)] public string? Carbon { get; set; }
    [StringLength(100)] public string? Silicon { get; set; }
    [StringLength(100)] public string? Manganese { get; set; }
    [StringLength(100)] public string? Phosphorus { get; set; }
    [StringLength(100)] public string? Sulfur { get; set; }
    [StringLength(100)] public string? Nickel { get; set; }
    [StringLength(100)] public string? Chromium { get; set; }
    [StringLength(100)] public string? Molybdenum { get; set; }
    [StringLength(100)] public string? Copper { get; set; }
    [StringLength(100)] public string? Nitrogen { get; set; }
    [StringLength(100)] public string? Niobium { get; set; }
    [StringLength(100)] public string? Titanium { get; set; }
    [StringLength(100)] public string? Iron { get; set; }
    [StringLength(100)] public string? Aluminum { get; set; }
    [StringLength(100)] public string? Tungsten { get; set; }
}

public class UpdateGradeChemicalCompositionRequest
{
    [Required(ErrorMessage = "标准牌号不能为空")]
    [StringLength(50, ErrorMessage = "标准牌号长度不能超过50")]
    public string StandardGrade { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "标准牌号类别长度不能超过50")]
    public string? StandardGradeCategory { get; set; }

    [StringLength(100)] public string? Carbon { get; set; }
    [StringLength(100)] public string? Silicon { get; set; }
    [StringLength(100)] public string? Manganese { get; set; }
    [StringLength(100)] public string? Phosphorus { get; set; }
    [StringLength(100)] public string? Sulfur { get; set; }
    [StringLength(100)] public string? Nickel { get; set; }
    [StringLength(100)] public string? Chromium { get; set; }
    [StringLength(100)] public string? Molybdenum { get; set; }
    [StringLength(100)] public string? Copper { get; set; }
    [StringLength(100)] public string? Nitrogen { get; set; }
    [StringLength(100)] public string? Niobium { get; set; }
    [StringLength(100)] public string? Titanium { get; set; }
    [StringLength(100)] public string? Iron { get; set; }
    [StringLength(100)] public string? Aluminum { get; set; }
    [StringLength(100)] public string? Tungsten { get; set; }
}
