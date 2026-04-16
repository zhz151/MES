// 文件路径: MES.Core/DTOs/UpdateGradeMappingRequest.cs

using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

/// <summary>
/// 更新牌号对照请求
/// </summary>
public class UpdateGradeMappingRequest
{
    /// <summary>
    /// 标准牌号
    /// </summary>
    [Required(ErrorMessage = "标准牌号不能为空")]
    [StringLength(50, ErrorMessage = "标准牌号长度不能超过50")]
    public string StandardGrade { get; set; } = string.Empty;

    /// <summary>
    /// 工厂牌号
    /// </summary>
    [Required(ErrorMessage = "工厂牌号不能为空")]
    [StringLength(50, ErrorMessage = "工厂牌号长度不能超过50")]
    public string PlantGrade { get; set; } = string.Empty;

    /// <summary>
    /// 密度
    /// </summary>
    public decimal? Density { get; set; }

    /// <summary>
    /// 热处理工艺
    /// </summary>
    [StringLength(100, ErrorMessage = "热处理工艺长度不能超过100")]
    public string? HeatTreatment { get; set; }

    /// <summary>
    /// 是否特殊材料
    /// </summary>
    public bool? SpecialMaterial { get; set; }

    /// <summary>
    /// 特殊注意事项
    /// </summary>
    [StringLength(500, ErrorMessage = "特殊注意事项长度不能超过500")]
    public string? SpecialNote { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [StringLength(500, ErrorMessage = "备注长度不能超过500")]
    public string? Remark { get; set; }
}