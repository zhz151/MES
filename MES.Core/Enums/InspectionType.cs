using System.ComponentModel.DataAnnotations;

namespace MES.Core.Enums;

/// <summary>
/// 成检类型
/// </summary>
public enum InspectionType
{
    /// <summary>预成检</summary>
    [Display(Name = "预成检")]
    PreInspection,

    /// <summary>正式成检</summary>
    [Display(Name = "正式成检")]
    FormalInspection,
}
