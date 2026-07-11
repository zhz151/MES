namespace MES.Core.DTOs.Materials;

/// <summary>
/// 原料采购计划 DTO
/// </summary>
public class PurchaseSemiPlanDto
{
    /// <summary>
    /// 计划ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 关联工单ID
    /// </summary>
    public int WorkOrderId { get; set; }

    /// <summary>
    /// 计划日期
    /// </summary>
    public DateTime PlanDate { get; set; }

    /// <summary>
    /// 调整壁厚(成品)(mm)
    /// </summary>
    public decimal AdjustedWallThickness { get; set; }

    /// <summary>
    /// 成材率(%)
    /// </summary>
    public decimal YieldRate { get; set; }

    /// <summary>
    /// 投料倍率(1制几)
    /// </summary>
    public int InputMultiple { get; set; }

    /// <summary>
    /// 正品率(%)
    /// </summary>
    public decimal QualifiedRate { get; set; }

    /// <summary>
    /// 密度(g/cm³)
    /// </summary>
    public decimal? Density { get; set; }

    /// <summary>
    /// 成品单重(kg/支)
    /// </summary>
    public decimal? UnitWeight { get; set; }

    /// <summary>
    /// 原料单重(kg/支)
    /// </summary>
    public decimal? RawUnitWeight { get; set; }

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 原料类型
    /// </summary>
    public string RawMaterialType { get; set; } = null!;

    /// <summary>
    /// 原料规格
    /// </summary>
    public string RawMaterialSpec { get; set; } = null!;

    /// <summary>
    /// 需求单重(kg/支)
    /// </summary>
    public decimal? RequiredUnitWeight { get; set; }

    /// <summary>
    /// 需求支数
    /// </summary>
    public int? RequiredPieces { get; set; }

    /// <summary>
    /// 需求重量(kg)
    /// </summary>
    public decimal RequiredWeight { get; set; }

    /// <summary>
    /// 要求到货日期
    /// </summary>
    public DateTime RequiredDate { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>工艺周期（天）</summary>
    public int StandardCycle { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public string CreatedBy { get; set; } = null!;
}
