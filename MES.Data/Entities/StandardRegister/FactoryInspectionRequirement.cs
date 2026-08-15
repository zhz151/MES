namespace MES.Data.Entities.StandardRegister;

/// <summary>
/// 工厂检验项要求 — 按标准号列出工厂各检验项目的强制性等级（必检/按需等）
/// 与"标准号检验项要求"的区别：增加了 PMI检验/表检/尺寸/内窥/水下气压/端口着色 6 项，且列顺序按工厂实际检验流程排列
/// </summary>
public class FactoryInspectionRequirement : BaseEntity
{
    /// <summary>标准号</summary>
    public string StandardNo { get; set; } = null!;

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
