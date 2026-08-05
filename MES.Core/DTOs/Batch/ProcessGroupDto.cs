namespace MES.Core.DTOs.Batch;

/// <summary>
/// 工序组DTO
/// </summary>
public class ProcessGroupDto
{
    public int Id { get; set; }
    public int ProductionBatchId { get; set; }
    public int SequenceNumber { get; set; }
    public string ProcessName { get; set; } = null!;
    public string? ManufacturingSpec { get; set; }
    public string? OuterDiameterTolerance { get; set; }
    public string? WallThicknessTolerance { get; set; }
    public string? ManufacturingLength { get; set; }
    public string? CuttingTreatment { get; set; }
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

    // 审计字段
    public DateTimeOffset CreatedTime { get; set; }
    public string CreatedBy { get; set; } = null!;
}
