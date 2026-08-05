using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs.Batch;

/// <summary>
/// 创建工序组请求
/// </summary>
public class CreateProcessGroupRequest
{
    [Required(ErrorMessage = "工序名称不能为空")]
    [MaxLength(50)]
    public string ProcessName { get; set; } = null!;

    [MaxLength(100)]
    public string? ManufacturingSpec { get; set; }

    [MaxLength(50)]
    public string? OuterDiameterTolerance { get; set; }

    [MaxLength(50)]
    public string? WallThicknessTolerance { get; set; }

    [MaxLength(100)]
    public string? ManufacturingLength { get; set; }

    [MaxLength(200)]
    public string? CuttingTreatment { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    // 26个工段
    public int? ColdRollDraw { get; set; }
    public int? OilPipeCut { get; set; }
    public int? Degrease { get; set; }
    public int? EmulsionWash { get; set; }
    public int? UltrasonicWash { get; set; }
    public int? ClothPolish { get; set; }
    public int? BrightAnnealing { get; set; }
    public int? Solution { get; set; }
    public int? Straighten { get; set; }
    public int? Cut { get; set; }
    public int? ThicknessMeasure { get; set; }
    public int? Pickle { get; set; }
    public int? OuterPolish { get; set; }
    public int? InnerPolish { get; set; }
    public int? InnerGrinding { get; set; }
    public int? OuterSpotGrinding { get; set; }
    public int? SandBlasting { get; set; }
    public int? ShotBlasting { get; set; }
    public int? Inspection { get; set; }
    public int? WeldingHead { get; set; }
    public int? Welding { get; set; }
    public int? Lubrication { get; set; }
    public int? Packing { get; set; }
    public int? Warehouse { get; set; }
    public int? Extra1 { get; set; }
    public int? Extra2 { get; set; }
}
