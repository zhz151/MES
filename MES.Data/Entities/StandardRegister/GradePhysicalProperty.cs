namespace MES.Data.Entities.ProductionStandard;

/// <summary>
/// 牌号物理性能 — 按标准牌号+牌号类别的物理/力学性能参数
/// </summary>
public class GradePhysicalProperty : BaseEntity
{
    /// <summary>标准牌号</summary>
    public string StandardGrade { get; set; } = null!;

    /// <summary>标准牌号类别</summary>
    public string? StandardGradeCategory { get; set; }

    /// <summary>密度(g/cm³)</summary>
    public decimal Density { get; set; }

    /// <summary>热处理温度</summary>
    public string? HeatTreatmentTemp { get; set; }

    /// <summary>硬度洛氏(HRB/HRC)</summary>
    public string? HardnessRockwell { get; set; }

    /// <summary>硬度维氏(HV)</summary>
    public string? HardnessVickers { get; set; }

    /// <summary>硬度布氏(HB)</summary>
    public string? HardnessBrinell { get; set; }

    /// <summary>抗拉强度(MPa)</summary>
    public string? TensileStrength { get; set; }

    /// <summary>屈服强度0.2(MPa)</summary>
    public string? YieldStrength02 { get; set; }

    /// <summary>屈服强度1.0(MPa)</summary>
    public string? YieldStrength10 { get; set; }

    /// <summary>延伸率(%)</summary>
    public string? Elongation { get; set; }

    /// <summary>晶粒度</summary>
    public string? GrainSize { get; set; }
}
