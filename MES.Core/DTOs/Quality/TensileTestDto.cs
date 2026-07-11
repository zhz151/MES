using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs.Quality;

/// <summary>
/// 室温拉伸检验DTO
/// </summary>
public class TensileTestDto
{
    public int Id { get; set; }
    public DateTime InspectionDate { get; set; }
    public string Inspector { get; set; } = null!;
    public string FurnaceNo { get; set; } = null!;
    public string Grade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public int? SampleNo { get; set; }
    public string? SampleSize { get; set; }
    public string? InspectionStandard { get; set; }
    public decimal? OriginalGaugeLength { get; set; }
    public decimal? FinalGaugeLength { get; set; }
    public decimal? TensileStrength { get; set; }
    public decimal? YieldStrengthRp02 { get; set; }
    public decimal? YieldStrengthRp1 { get; set; }
    public decimal? Elongation { get; set; }
    public string? Judgment { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 创建室温拉伸检验请求
/// </summary>
public class CreateTensileTestRequest
{
    [Required(ErrorMessage = "检验日期不能为空")]
    public DateTime InspectionDate { get; set; }

    [Required(ErrorMessage = "检验员不能为空")]
    [MaxLength(50)]
    public string Inspector { get; set; } = string.Empty;

    [Required(ErrorMessage = "生产编号不能为空")]
    [MaxLength(50)]
    public string FurnaceNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "牌号不能为空")]
    [MaxLength(50)]
    public string Grade { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Specification { get; set; } = string.Empty;

    public int? SampleNo { get; set; }

    [MaxLength(50)]
    public string? SampleSize { get; set; }

    [MaxLength(100)]
    public string? InspectionStandard { get; set; }

    public decimal? OriginalGaugeLength { get; set; }

    public decimal? FinalGaugeLength { get; set; }

    public decimal? TensileStrength { get; set; }

    public decimal? YieldStrengthRp02 { get; set; }

    public decimal? YieldStrengthRp1 { get; set; }

    public decimal? Elongation { get; set; }

    [MaxLength(50)]
    public string? Judgment { get; set; }
}

/// <summary>
/// 更新室温拉伸检验请求
/// </summary>
public class UpdateTensileTestRequest
{
    [Required(ErrorMessage = "检验日期不能为空")]
    public DateTime InspectionDate { get; set; }

    [MaxLength(50)]
    public string? Inspector { get; set; }

    [MaxLength(50)]
    public string? FurnaceNo { get; set; }

    [MaxLength(50)]
    public string? Grade { get; set; }

    [MaxLength(100)]
    public string? Specification { get; set; }

    public int? SampleNo { get; set; }

    [MaxLength(50)]
    public string? SampleSize { get; set; }

    [MaxLength(100)]
    public string? InspectionStandard { get; set; }

    public decimal? OriginalGaugeLength { get; set; }

    public decimal? FinalGaugeLength { get; set; }

    public decimal? TensileStrength { get; set; }

    public decimal? YieldStrengthRp02 { get; set; }

    public decimal? YieldStrengthRp1 { get; set; }

    public decimal? Elongation { get; set; }

    [MaxLength(50)]
    public string? Judgment { get; set; }
}
