using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs.Quality;

/// <summary>
/// 硬度检验DTO
/// </summary>
public class HardnessTestDto
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
    public string? HardnessMode { get; set; }
    public string? HardnessValue { get; set; }
    public string? Judgment { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 创建硬度检验请求
/// </summary>
public class CreateHardnessTestRequest
{
    [Required(ErrorMessage = "检验日期不能为空")]
    public DateTime InspectionDate { get; set; }

    [Required(ErrorMessage = "检验员不能为空")]
    [MaxLength(50)]
    public string Inspector { get; set; } = string.Empty;

    [Required(ErrorMessage = "炉批号不能为空")]
    [MaxLength(50)]
    public string FurnaceNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "牌号不能为空")]
    [MaxLength(50)]
    public string Grade { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Specification { get; set; } = string.Empty;

    public int? SampleNo { get; set; }

    [MaxLength(100)]
    public string? SampleSize { get; set; }

    [MaxLength(100)]
    public string? InspectionStandard { get; set; }

    [MaxLength(50)]
    public string? HardnessMode { get; set; }

    [MaxLength(50)]
    public string? HardnessValue { get; set; }

    [MaxLength(20)]
    public string? Judgment { get; set; }
}

/// <summary>
/// 更新硬度检验请求
/// </summary>
public class UpdateHardnessTestRequest
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

    [MaxLength(100)]
    public string? SampleSize { get; set; }

    [MaxLength(100)]
    public string? InspectionStandard { get; set; }

    [MaxLength(50)]
    public string? HardnessMode { get; set; }

    [MaxLength(50)]
    public string? HardnessValue { get; set; }

    [MaxLength(20)]
    public string? Judgment { get; set; }
}
