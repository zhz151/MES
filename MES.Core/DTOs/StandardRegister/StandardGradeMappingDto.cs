namespace MES.Core.DTOs.StandardRegister;

/// <summary>
/// 标准牌号映射 DTO
/// </summary>
public class StandardGradeMappingDto
{
    /// <summary>
    /// 映射ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 标准牌号
    /// </summary>
    public string StandardGrade { get; set; } = string.Empty;

    /// <summary>
    /// 标准牌号类别
    /// </summary>
    public string? StandardGradeCategory { get; set; }

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string PlantGrade { get; set; } = string.Empty;

    /// <summary>
    /// 密度(g/cm³)
    /// </summary>
    public decimal Density { get; set; }

    /// <summary>
    /// 热处理工艺
    /// </summary>
    public string? HeatTreatment { get; set; }

    /// <summary>
    /// 是否特殊材料
    /// </summary>
    public bool SpecialMaterial { get; set; }

    /// <summary>
    /// 特殊注意事项
    /// </summary>
    public string? SpecialNote { get; set; }

    /// <summary>
    /// 钢性
    /// </summary>
    public string SteelProperty { get; set; } = string.Empty;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
