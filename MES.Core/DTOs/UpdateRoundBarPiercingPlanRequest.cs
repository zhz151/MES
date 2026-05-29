namespace MES.Core.DTOs;

/// <summary>
/// 更新圆棒穿孔计划请求
/// </summary>
public class UpdateRoundBarPiercingPlanRequest
{
    public DateTime PlanDate { get; set; }

    // 测算参数
    public decimal AdjustedWallThickness { get; set; }
    public decimal YieldRate { get; set; }
    public int InputMultiple { get; set; } = 1;
    public decimal QualifiedRate { get; set; }

    // 采购信息
    public string PlantGrade { get; set; } = null!;
    public string RawMaterialType { get; set; } = null!;
    public string RoundBarSpec { get; set; } = null!;
    public string PiercingSpec { get; set; } = null!;
    public decimal? RequiredUnitWeight { get; set; }
    public int? RequiredPieces { get; set; }
    public decimal RequiredWeight { get; set; }
    public DateTime RequiredDate { get; set; }

    // 工序组
    public List<SavePlanProcessGroupItem>? ProcessGroups { get; set; }

    // 其他
    public string? Remark { get; set; }
}
