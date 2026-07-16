using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs.StandardRegister;

public class GradePhysicalPropertyDto
{
    public int Id { get; set; }
    public string StandardGrade { get; set; } = string.Empty;
    public string? StandardGradeCategory { get; set; }
    public decimal Density { get; set; }
    public string? HeatTreatmentTemp { get; set; }
    public string? HardnessRockwell { get; set; }
    public string? HardnessVickers { get; set; }
    public string? HardnessBrinell { get; set; }
    public string? TensileStrength { get; set; }
    public string? YieldStrength02 { get; set; }
    public string? YieldStrength10 { get; set; }
    public string? Elongation { get; set; }
    public string? GrainSize { get; set; }
}

public class CreateGradePhysicalPropertyRequest
{
    [Required(ErrorMessage = "标准牌号不能为空")]
    [StringLength(50, ErrorMessage = "标准牌号长度不能超过50")]
    public string StandardGrade { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "标准牌号类别长度不能超过50")]
    public string? StandardGradeCategory { get; set; }

    [Required(ErrorMessage = "密度不能为空")]
    [Range(0.0001, 99.9999, ErrorMessage = "密度必须在0.0001到99.9999之间")]
    public decimal Density { get; set; }

    [StringLength(100)] public string? HeatTreatmentTemp { get; set; }
    [StringLength(100)] public string? HardnessRockwell { get; set; }
    [StringLength(100)] public string? HardnessVickers { get; set; }
    [StringLength(100)] public string? HardnessBrinell { get; set; }
    [StringLength(100)] public string? TensileStrength { get; set; }
    [StringLength(100)] public string? YieldStrength02 { get; set; }
    [StringLength(100)] public string? YieldStrength10 { get; set; }
    [StringLength(100)] public string? Elongation { get; set; }
    [StringLength(100)] public string? GrainSize { get; set; }
}

public class UpdateGradePhysicalPropertyRequest
{
    [Required(ErrorMessage = "标准牌号不能为空")]
    [StringLength(50, ErrorMessage = "标准牌号长度不能超过50")]
    public string StandardGrade { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "标准牌号类别长度不能超过50")]
    public string? StandardGradeCategory { get; set; }

    public decimal? Density { get; set; }

    [StringLength(100)] public string? HeatTreatmentTemp { get; set; }
    [StringLength(100)] public string? HardnessRockwell { get; set; }
    [StringLength(100)] public string? HardnessVickers { get; set; }
    [StringLength(100)] public string? HardnessBrinell { get; set; }
    [StringLength(100)] public string? TensileStrength { get; set; }
    [StringLength(100)] public string? YieldStrength02 { get; set; }
    [StringLength(100)] public string? YieldStrength10 { get; set; }
    [StringLength(100)] public string? Elongation { get; set; }
    [StringLength(100)] public string? GrainSize { get; set; }
}
