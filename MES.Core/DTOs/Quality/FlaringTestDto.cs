using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs.Quality;

/// <summary>
/// 扩口检验DTO
/// </summary>
public class FlaringTestDto
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
    public string? MandrelTaper { get; set; }
    public decimal? FlaredDiameter { get; set; }
    public decimal? FlaringRate { get; set; }
    public string? Observation { get; set; }
    public string? Judgment { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 创建扩口检验请求
/// </summary>
public class CreateFlaringTestRequest
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

    [MaxLength(50)]
    public string? MandrelTaper { get; set; }

    public decimal? FlaredDiameter { get; set; }

    public decimal? FlaringRate { get; set; }

    [MaxLength(200)]
    public string? Observation { get; set; }

    [MaxLength(50)]
    public string? Judgment { get; set; }
}

/// <summary>
/// 更新扩口检验请求
/// </summary>
public class UpdateFlaringTestRequest
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

    [MaxLength(50)]
    public string? MandrelTaper { get; set; }

    public decimal? FlaredDiameter { get; set; }

    public decimal? FlaringRate { get; set; }

    [MaxLength(200)]
    public string? Observation { get; set; }

    [MaxLength(50)]
    public string? Judgment { get; set; }
}
