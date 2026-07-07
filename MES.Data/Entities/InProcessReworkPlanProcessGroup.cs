namespace MES.Data.Entities;

/// <summary>
/// 在产改制计划工序组 — 对应 InProcessReworkPlan 的工艺路线
/// </summary>
public class InProcessReworkPlanProcessGroup : BaseEntity
{
    /// <summary>
    /// 关联在产改制计划ID
    /// </summary>
    public int InProcessReworkPlanId { get; set; }

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
    public int ManufacturingMultiple { get; set; }
    public string? Remark { get; set; }

    // ========== 工段 ==========

    public int? ColdRollDraw { get; set; }
    public int? OilPipeCut { get; set; }
    public int? Degrease { get; set; }
    public int? Solution { get; set; }
    public int? Straighten { get; set; }
    public int? Cut { get; set; }
    public int? ThicknessMeasure { get; set; }
    public int? Pickle { get; set; }
    public int? OuterPolish { get; set; }
    public int? InnerGrinding { get; set; }
    public int? OuterSpotGrinding { get; set; }
    public int? Inspection { get; set; }
    public int? WeldingHead { get; set; }
    public int? Lubrication { get; set; }
    public int? Warehouse { get; set; }
}
