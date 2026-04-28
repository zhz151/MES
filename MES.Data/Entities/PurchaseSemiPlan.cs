using MES.Core.Enums;

namespace MES.Data.Entities;

/// <summary>
/// 原料采购计划（外购荒管/半成品，含用料测算+工艺路线）
/// </summary>
public class PurchaseSemiPlan : BaseEntity
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

    /// <summary>
    /// 原料支数（定尺/范围尺=自动，非定尺=人工）
    /// </summary>
    public int? RequiredPieces { get; set; }

    /// <summary>
    /// 原料重量(kg)（定尺/范围尺=自动，非定尺=人工）
    /// </summary>
    public decimal RequiredWeight { get; set; }

    // ========== 采购信息 ==========

    /// <summary>
    /// 原料类型（荒管/半成品）
    /// </summary>
    public RawMaterialType RawMaterialType { get; set; }

    /// <summary>
    /// 原料规格
    /// </summary>
    public string RawMaterialSpec { get; set; } = null!;

    /// <summary>
    /// 要求到货日期
    /// </summary>
    public DateTime? RequiredDate { get; set; }

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
