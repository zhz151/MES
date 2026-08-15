using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs.StandardRegister;

public class FactoryInspectionRequirementDto
{
    public int Id { get; set; }

    /// <summary>标准号</summary>
    public string StandardNo { get; set; } = string.Empty;

    /// <summary>化学分析(成品)</summary>
    public string? ChemicalComposition { get; set; }

    /// <summary>PMI检验</summary>
    public string? PmiInspection { get; set; }

    /// <summary>表检</summary>
    public string? SurfaceInspection { get; set; }

    /// <summary>尺寸</summary>
    public string? Dimension { get; set; }

    /// <summary>内窥</summary>
    public string? Endoscopy { get; set; }

    /// <summary>液压检验</summary>
    public string? HydrostaticTest { get; set; }

    /// <summary>水下气压</summary>
    public string? UnderwaterPressure { get; set; }

    /// <summary>涡流探伤</summary>
    public string? EddyCurrent { get; set; }

    /// <summary>超声波检验</summary>
    public string? UltrasonicTest { get; set; }

    /// <summary>端口着色</summary>
    public string? PortColoring { get; set; }

    /// <summary>射线探伤</summary>
    public string? RadiographicTest { get; set; }

    /// <summary>硬度(洛氏)</summary>
    public string? HardnessRockwell { get; set; }

    /// <summary>硬度(布氏)</summary>
    public string? HardnessBrinell { get; set; }

    /// <summary>硬度(维氏)</summary>
    public string? HardnessVickers { get; set; }

    /// <summary>拉伸(室温)</summary>
    public string? TensileRoomTemp { get; set; }

    /// <summary>拉伸(高温)</summary>
    public string? TensileHighTemp { get; set; }

    /// <summary>焊接接头拉伸</summary>
    public string? WeldJointTensile { get; set; }

    /// <summary>冲击试验</summary>
    public string? ImpactTest { get; set; }

    /// <summary>焊接接头冲击</summary>
    public string? WeldJointImpact { get; set; }

    /// <summary>压扁试验</summary>
    public string? FlatteningTest { get; set; }

    /// <summary>卷边试验</summary>
    public string? FlaringTest { get; set; }

    /// <summary>扩口试验</summary>
    public string? ExpandingTest { get; set; }

    /// <summary>弯曲试验</summary>
    public string? BendTest { get; set; }

    /// <summary>焊接接头弯曲</summary>
    public string? WeldJointBend { get; set; }

    /// <summary>晶粒度</summary>
    public string? GrainSize { get; set; }

    /// <summary>晶间腐蚀</summary>
    public string? IntergranularCorrosion { get; set; }

    /// <summary>点腐蚀</summary>
    public string? PittingCorrosion { get; set; }

    /// <summary>金相检验</summary>
    public string? FerriteContent { get; set; }

    /// <summary>低倍组织</summary>
    public string? Macrostructure { get; set; }
}

public class CreateFactoryInspectionRequirementRequest
{
    [Required(ErrorMessage = "标准号不能为空")]
    [StringLength(100, ErrorMessage = "标准号长度不能超过100")]
    public string StandardNo { get; set; } = string.Empty;

    [StringLength(200)] public string? ChemicalComposition { get; set; }
    [StringLength(200)] public string? PmiInspection { get; set; }
    [StringLength(200)] public string? SurfaceInspection { get; set; }
    [StringLength(200)] public string? Dimension { get; set; }
    [StringLength(200)] public string? Endoscopy { get; set; }
    [StringLength(200)] public string? HydrostaticTest { get; set; }
    [StringLength(200)] public string? UnderwaterPressure { get; set; }
    [StringLength(200)] public string? EddyCurrent { get; set; }
    [StringLength(200)] public string? UltrasonicTest { get; set; }
    [StringLength(200)] public string? PortColoring { get; set; }
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

public class UpdateFactoryInspectionRequirementRequest
{
    [Required(ErrorMessage = "标准号不能为空")]
    [StringLength(100, ErrorMessage = "标准号长度不能超过100")]
    public string StandardNo { get; set; } = string.Empty;

    [StringLength(200)] public string? ChemicalComposition { get; set; }
    [StringLength(200)] public string? PmiInspection { get; set; }
    [StringLength(200)] public string? SurfaceInspection { get; set; }
    [StringLength(200)] public string? Dimension { get; set; }
    [StringLength(200)] public string? Endoscopy { get; set; }
    [StringLength(200)] public string? HydrostaticTest { get; set; }
    [StringLength(200)] public string? UnderwaterPressure { get; set; }
    [StringLength(200)] public string? EddyCurrent { get; set; }
    [StringLength(200)] public string? UltrasonicTest { get; set; }
    [StringLength(200)] public string? PortColoring { get; set; }
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
