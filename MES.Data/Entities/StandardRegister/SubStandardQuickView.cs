namespace MES.Data.Entities.StandardRegister;

/// <summary>
/// 子标准速览 — 按标准号列出各检验项目对应的引用标准/方法
/// </summary>
public class SubStandardQuickView : BaseEntity
{
    /// <summary>标准号</summary>
    public string StandardNo { get; set; } = null!;

    /// <summary>化学成分</summary>
    public string? ChemicalComposition { get; set; }

    /// <summary>液密检验</summary>
    public string? HydrostaticTest { get; set; }

    /// <summary>涡流探伤</summary>
    public string? EddyCurrent { get; set; }

    /// <summary>超声波检验</summary>
    public string? UltrasonicTest { get; set; }

    /// <summary>射线探伤</summary>
    public string? RadiographicTest { get; set; }

    /// <summary>硬度试验(洛氏)</summary>
    public string? HardnessRockwell { get; set; }

    /// <summary>硬度试验(布氏)</summary>
    public string? HardnessBrinell { get; set; }

    /// <summary>硬度试验(维氏)</summary>
    public string? HardnessVickers { get; set; }

    /// <summary>拉伸试验(室温)</summary>
    public string? TensileRoomTemp { get; set; }

    /// <summary>拉伸试验(高温)</summary>
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

    /// <summary>铁素体含量</summary>
    public string? FerriteContent { get; set; }

    /// <summary>低倍组织</summary>
    public string? Macrostructure { get; set; }
}
