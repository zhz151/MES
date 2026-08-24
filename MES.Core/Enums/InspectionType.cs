using System.ComponentModel.DataAnnotations;

namespace MES.Core.Enums;

/// <summary>
/// 成检类型
/// </summary>
public enum InspectionType
{
    /// <summary>预检</summary>
    [Display(Name = "预检")]
    PreInspection,

    /// <summary>终检</summary>
    [Display(Name = "终检")]
    FormalInspection,
}
