using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs.Quality;

/// <summary>
/// 晶间腐蚀检验DTO
/// </summary>
public class IntergranularCorrosionTestDto
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
    public string? SensitizationTemperature { get; set; }
    public string? SensitizationDuration { get; set; }
    public string? CorrosionSolution { get; set; }
    public string? CorrosionTime { get; set; }
    public string? BendDegree { get; set; }
    public string? Magnification { get; set; }
    public string? ObservationResult { get; set; }
    public string? Judgment { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 创建晶间腐蚀检验请求
/// </summary>
public class CreateIntergranularCorrosionTestRequest
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

    [MaxLength(50)]
    public string? SensitizationTemperature { get; set; }

    [MaxLength(50)]
    public string? SensitizationDuration { get; set; }

    [MaxLength(100)]
    public string? CorrosionSolution { get; set; }

    [MaxLength(50)]
    public string? CorrosionTime { get; set; }

    [MaxLength(50)]
    public string? BendDegree { get; set; }

    [MaxLength(50)]
    public string? Magnification { get; set; }

    [MaxLength(200)]
    public string? ObservationResult { get; set; }

    [MaxLength(50)]
    public string? Judgment { get; set; }
}

/// <summary>
/// 更新晶间腐蚀检验请求
/// </summary>
public class UpdateIntergranularCorrosionTestRequest
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
    public string? SensitizationTemperature { get; set; }

    [MaxLength(50)]
    public string? SensitizationDuration { get; set; }

    [MaxLength(100)]
    public string? CorrosionSolution { get; set; }

    [MaxLength(50)]
    public string? CorrosionTime { get; set; }

    [MaxLength(50)]
    public string? BendDegree { get; set; }

    [MaxLength(50)]
    public string? Magnification { get; set; }

    [MaxLength(200)]
    public string? ObservationResult { get; set; }

    [MaxLength(50)]
    public string? Judgment { get; set; }
}
