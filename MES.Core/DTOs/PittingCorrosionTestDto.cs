using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

/// <summary>
/// 点腐蚀检验DTO
/// </summary>
public class PittingCorrosionTestDto
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
    public string? PolishingGrade { get; set; }
    public decimal? RawWeight { get; set; }
    public string? CorrosionSolution { get; set; }
    public string? CorrosionTemperature { get; set; }
    public string? CorrosionTime { get; set; }
    public decimal? FinalWeight { get; set; }
    public decimal? CorrosionRate { get; set; }
    public decimal? MaxPitDepth { get; set; }
    public string? Judgment { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 创建点腐蚀检验请求
/// </summary>
public class CreatePittingCorrosionTestRequest
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

    [Required(ErrorMessage = "规格不能为空")]
    [MaxLength(100)]
    public string Specification { get; set; } = string.Empty;

    public int? SampleNo { get; set; }

    [MaxLength(50)]
    public string? SampleSize { get; set; }

    [MaxLength(100)]
    public string? InspectionStandard { get; set; }

    [MaxLength(100)]
    public string? PolishingGrade { get; set; }

    public decimal? RawWeight { get; set; }

    [MaxLength(100)]
    public string? CorrosionSolution { get; set; }

    [MaxLength(50)]
    public string? CorrosionTemperature { get; set; }

    [MaxLength(50)]
    public string? CorrosionTime { get; set; }

    public decimal? FinalWeight { get; set; }

    public decimal? CorrosionRate { get; set; }

    public decimal? MaxPitDepth { get; set; }

    [MaxLength(50)]
    public string? Judgment { get; set; }
}

/// <summary>
/// 更新点腐蚀检验请求
/// </summary>
public class UpdatePittingCorrosionTestRequest
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

    [MaxLength(100)]
    public string? PolishingGrade { get; set; }

    public decimal? RawWeight { get; set; }

    [MaxLength(100)]
    public string? CorrosionSolution { get; set; }

    [MaxLength(50)]
    public string? CorrosionTemperature { get; set; }

    [MaxLength(50)]
    public string? CorrosionTime { get; set; }

    public decimal? FinalWeight { get; set; }

    public decimal? CorrosionRate { get; set; }

    public decimal? MaxPitDepth { get; set; }

    [MaxLength(50)]
    public string? Judgment { get; set; }
}
