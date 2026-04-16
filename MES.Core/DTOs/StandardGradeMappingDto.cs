// 文件路径: MES.Core/DTOs/StandardGradeMappingDto.cs

namespace MES.Core.DTOs;

/// <summary>
/// 牌号对照 DTO
/// </summary>
public class StandardGradeMappingDto
{
    /// <summary>
    /// 主键
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 标准牌号
    /// </summary>
    public string StandardGrade { get; set; } = string.Empty;

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string PlantGrade { get; set; } = string.Empty;

    /// <summary>
    /// 密度
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
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}