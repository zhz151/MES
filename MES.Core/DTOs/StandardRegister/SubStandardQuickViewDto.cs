using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs.StandardRegister;

public class SubStandardQuickViewDto
{
    public int Id { get; set; }

    /// <summary>标准号</summary>
    public string StandardNo { get; set; } = string.Empty;

    public string? ChemicalComposition { get; set; }
    public string? HydrostaticTest { get; set; }
    public string? EddyCurrent { get; set; }
    public string? UltrasonicTest { get; set; }
    public string? RadiographicTest { get; set; }
    public string? HardnessRockwell { get; set; }
    public string? HardnessBrinell { get; set; }
    public string? HardnessVickers { get; set; }
    public string? TensileRoomTemp { get; set; }
    public string? TensileHighTemp { get; set; }
    public string? WeldJointTensile { get; set; }
    public string? ImpactTest { get; set; }
    public string? WeldJointImpact { get; set; }
    public string? FlatteningTest { get; set; }
    public string? FlaringTest { get; set; }
    public string? ExpandingTest { get; set; }
    public string? BendTest { get; set; }
    public string? WeldJointBend { get; set; }
    public string? GrainSize { get; set; }
    public string? IntergranularCorrosion { get; set; }
    public string? PittingCorrosion { get; set; }
    public string? FerriteContent { get; set; }
    public string? Macrostructure { get; set; }
}

public class CreateSubStandardQuickViewRequest
{
    [Required(ErrorMessage = "标准号不能为空")]
    [StringLength(100, ErrorMessage = "标准号长度不能超过100")]
    public string StandardNo { get; set; } = string.Empty;

    [StringLength(200)] public string? ChemicalComposition { get; set; }
    [StringLength(200)] public string? HydrostaticTest { get; set; }
    [StringLength(200)] public string? EddyCurrent { get; set; }
    [StringLength(200)] public string? UltrasonicTest { get; set; }
    [StringLength(200)] public string? RadiographicTest { get; set; }
    [StringLength(200)] public string? HardnessRockwell { get; set; }
    [StringLength(200)] public string? HardnessBrinell { get; set; }
    [StringLength(200)] public string? HardnessVickers { get; set; }
    [StringLength(200)] public string? TensileRoomTemp { get; set; }
    [StringLength(200)] public string? TensileHighTemp { get; set; }
    [StringLength(200)] public string? WeldJointTensile { get; set; }
    [StringLength(200)] public string? ImpactTest { get; set; }
    [StringLength(200)] public string? WeldJointImpact { get; set; }
    [StringLength(200)] public string? FlatteningTest { get; set; }
    [StringLength(200)] public string? FlaringTest { get; set; }
    [StringLength(200)] public string? ExpandingTest { get; set; }
    [StringLength(200)] public string? BendTest { get; set; }
    [StringLength(200)] public string? WeldJointBend { get; set; }
    [StringLength(200)] public string? GrainSize { get; set; }
    [StringLength(200)] public string? IntergranularCorrosion { get; set; }
    [StringLength(200)] public string? PittingCorrosion { get; set; }
    [StringLength(200)] public string? FerriteContent { get; set; }
    [StringLength(200)] public string? Macrostructure { get; set; }
}

public class UpdateSubStandardQuickViewRequest
{
    [Required(ErrorMessage = "标准号不能为空")]
    [StringLength(100, ErrorMessage = "标准号长度不能超过100")]
    public string StandardNo { get; set; } = string.Empty;

    [StringLength(200)] public string? ChemicalComposition { get; set; }
    [StringLength(200)] public string? HydrostaticTest { get; set; }
    [StringLength(200)] public string? EddyCurrent { get; set; }
    [StringLength(200)] public string? UltrasonicTest { get; set; }
    [StringLength(200)] public string? RadiographicTest { get; set; }
    [StringLength(200)] public string? HardnessRockwell { get; set; }
    [StringLength(200)] public string? HardnessBrinell { get; set; }
    [StringLength(200)] public string? HardnessVickers { get; set; }
    [StringLength(200)] public string? TensileRoomTemp { get; set; }
    [StringLength(200)] public string? TensileHighTemp { get; set; }
    [StringLength(200)] public string? WeldJointTensile { get; set; }
    [StringLength(200)] public string? ImpactTest { get; set; }
    [StringLength(200)] public string? WeldJointImpact { get; set; }
    [StringLength(200)] public string? FlatteningTest { get; set; }
    [StringLength(200)] public string? FlaringTest { get; set; }
    [StringLength(200)] public string? ExpandingTest { get; set; }
    [StringLength(200)] public string? BendTest { get; set; }
    [StringLength(200)] public string? WeldJointBend { get; set; }
    [StringLength(200)] public string? GrainSize { get; set; }
    [StringLength(200)] public string? IntergranularCorrosion { get; set; }
    [StringLength(200)] public string? PittingCorrosion { get; set; }
    [StringLength(200)] public string? FerriteContent { get; set; }
    [StringLength(200)] public string? Macrostructure { get; set; }
}
