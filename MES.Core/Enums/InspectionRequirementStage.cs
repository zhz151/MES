using System.ComponentModel.DataAnnotations;

namespace MES.Core.Enums;

/// <summary>
/// 技术要求检验项阶段（订单技术要求中成品检验项要求的检验阶段）
/// 终=仅正式成检；预=仅预成检；预+终=预成检与正式成检均需；-=不要求
/// </summary>
public enum InspectionRequirementStage
{
    /// <summary>不要求（-）</summary>
    [Display(Name = "-")]
    None = 0,

    /// <summary>仅正式成检（终）</summary>
    [Display(Name = "终")]
    FinalOnly = 1,

    /// <summary>仅预成检（预）</summary>
    [Display(Name = "预")]
    PreOnly = 2,

    /// <summary>预成检 + 正式成检均需（预+终）</summary>
    [Display(Name = "预+终")]
    PreAndFinal = 3,
}
