using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

/// <summary>
/// 化学分析DTO
/// </summary>
public class ChemicalAnalysisDto
{
    public int Id { get; set; }

    /// <summary>分析日期</summary>
    public DateTime AnalysisDate { get; set; }

    /// <summary>分析员</summary>
    public string Analyst { get; set; } = null!;

    /// <summary>炉号</summary>
    public string FurnaceNo { get; set; } = null!;

    /// <summary>牌号</summary>
    public string Grade { get; set; } = null!;

    /// <summary>分析次数</summary>
    public int? AnalysisCount { get; set; }

    /// <summary>分析标准</summary>
    public string? AnalysisStandard { get; set; }

    // ===== 化学元素含量 =====
    public decimal? C { get; set; }
    public decimal? Si { get; set; }
    public decimal? Mn { get; set; }
    public decimal? P { get; set; }
    public decimal? S { get; set; }
    public decimal? Ni { get; set; }
    public decimal? Cr { get; set; }
    public decimal? Mo { get; set; }
    public decimal? Cu { get; set; }
    public decimal? N { get; set; }
    public decimal? Nb { get; set; }
    public decimal? Ti { get; set; }
    public decimal? Fe { get; set; }
    public decimal? Al { get; set; }
    public decimal? W { get; set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 创建化学分析请求
/// </summary>
public class CreateChemicalAnalysisRequest
{
    [Required(ErrorMessage = "分析日期不能为空")]
    public DateTime AnalysisDate { get; set; }

    [Required(ErrorMessage = "分析员不能为空")]
    [MaxLength(50)]
    public string Analyst { get; set; } = string.Empty;

    [Required(ErrorMessage = "炉号不能为空")]
    [MaxLength(50)]
    public string FurnaceNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "牌号不能为空")]
    [MaxLength(50)]
    public string Grade { get; set; } = string.Empty;

    public int? AnalysisCount { get; set; }

    [MaxLength(100)]
    public string? AnalysisStandard { get; set; }

    public decimal? C { get; set; }
    public decimal? Si { get; set; }
    public decimal? Mn { get; set; }
    public decimal? P { get; set; }
    public decimal? S { get; set; }
    public decimal? Ni { get; set; }
    public decimal? Cr { get; set; }
    public decimal? Mo { get; set; }
    public decimal? Cu { get; set; }
    public decimal? N { get; set; }
    public decimal? Nb { get; set; }
    public decimal? Ti { get; set; }
    public decimal? Fe { get; set; }
    public decimal? Al { get; set; }
    public decimal? W { get; set; }
}

/// <summary>
/// 更新化学分析请求（内联编辑用）
/// </summary>
public class UpdateChemicalAnalysisRequest
{
    [Required(ErrorMessage = "分析日期不能为空")]
    public DateTime AnalysisDate { get; set; }

    [MaxLength(50)]
    public string? Analyst { get; set; }

    [MaxLength(50)]
    public string? FurnaceNo { get; set; }

    [MaxLength(50)]
    public string? Grade { get; set; }

    public int? AnalysisCount { get; set; }

    [MaxLength(100)]
    public string? AnalysisStandard { get; set; }

    public decimal? C { get; set; }
    public decimal? Si { get; set; }
    public decimal? Mn { get; set; }
    public decimal? P { get; set; }
    public decimal? S { get; set; }
    public decimal? Ni { get; set; }
    public decimal? Cr { get; set; }
    public decimal? Mo { get; set; }
    public decimal? Cu { get; set; }
    public decimal? N { get; set; }
    public decimal? Nb { get; set; }
    public decimal? Ti { get; set; }
    public decimal? Fe { get; set; }
    public decimal? Al { get; set; }
    public decimal? W { get; set; }
}
