using MES.Core.Enums;

namespace MES.Data.Entities;

/// <summary>
/// 圆棒穿孔计划（圆棒原料经穿孔工序，含用料测算+工艺路线）
/// </summary>
public class RoundBarPiercingPlan : BaseEntity
{
    /// <summary>
    /// 关联工单ID
    /// </summary>
    public int WorkOrderId { get; set; }

    /// <summary>
    /// 计划日期
    /// </summary>
    public DateTime PlanDate { get; set; }

    // ========== 测算参数（人工填写） ==========

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

    // ========== 测算结果（自动计算） ==========

    /// <summary>
    /// 密度(g/cm³)
    /// </summary>
    public decimal? Density { get; set; }

    /// <summary>
    /// 成品单重(kg/支)（定尺/范围尺）
    /// </summary>
    public decimal? UnitWeight { get; set; }

    /// <summary>
    /// 原料单重(kg/支)
    /// </summary>
    public decimal? RawUnitWeight { get; set; }

    // ========== 采购信息 ==========

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 原料类型（圆棒）
    /// </summary>
    public RawMaterialType RawMaterialType { get; set; }

    /// <summary>
    /// 圆棒规格
    /// </summary>
    public string RoundBarSpec { get; set; } = null!;

    /// <summary>
    /// 穿孔规格
    /// </summary>
    public string PiercingSpec { get; set; } = null!;

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

    // ========== 工艺路线 ==========

    /// <summary>
    /// 工艺路线（JSON数组）
    /// [{"step":1,"spec":"67*5"},{"step":2,"spec":"38*3"},...]
    /// </summary>
    public string? ProcessPlan { get; set; }

    // ========== 其他 ==========

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
