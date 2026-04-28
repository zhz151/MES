namespace MES.Core.DTOs;

/// <summary>
/// 原料采购计划 DTO
/// </summary>
public class PurchaseSemiPlanDto
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public DateTime PlanDate { get; set; }

    // 测算参数
    public decimal AdjustedWallThickness { get; set; }
    public decimal YieldRate { get; set; }
    public int InputMultiple { get; set; }
    public decimal QualifiedRate { get; set; }

    // 测算结果
    public decimal? Density { get; set; }
    public decimal? UnitWeight { get; set; }
    public decimal? RawUnitWeight { get; set; }
    public int? RequiredPieces { get; set; }
    public decimal RequiredWeight { get; set; }

    // 采购信息
    public string RawMaterialType { get; set; } = null!;
    public string RawMaterialSpec { get; set; } = null!;
    public DateTime? RequiredDate { get; set; }

    // 工艺路线
    public string? ProcessPlan { get; set; }

    // 其他
    public string? Remark { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public string CreatedBy { get; set; } = null!;
}
