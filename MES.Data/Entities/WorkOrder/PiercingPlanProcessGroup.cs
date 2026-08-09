namespace MES.Data.Entities.WorkOrder;

/// <summary>
/// 圆棒穿孔计划工序组 — 对应 RoundBarPiercingPlan 的工艺路线
/// </summary>
public class PiercingPlanProcessGroup : BaseEntity
{
    /// <summary>
    /// 关联圆棒穿孔计划ID
    /// </summary>
    public int RoundBarPiercingPlanId { get; set; }

    /// <summary>
    /// 组内序号
    /// </summary>
    public int SequenceNumber { get; set; }

    // ========== 基础信息 ==========

    public string ProcessName { get; set; } = null!;
    public string? ManufacturingSpec { get; set; }
    public string? OuterDiameterTolerance { get; set; }
    public string? WallThicknessTolerance { get; set; }
    public string? ManufacturingLength { get; set; }
    public string? CuttingTreatment { get; set; }
    public string? Remark { get; set; }

    // ========== 工段 ==========

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
