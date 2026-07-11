using MES.Core.Enums;

namespace MES.Data.Entities.Materials;

/// <summary>
/// 外购成品计划（极简设计）
/// </summary>
public class PurchaseFinishedPlan : BaseEntity
{
    /// <summary>
    /// 关联工单ID
    /// </summary>
    public int WorkOrderId { get; set; }

    /// <summary>
    /// 计划日期
    /// </summary>
    public DateTime PlanDate { get; set; }

    /// <summary>
    /// 成品类型（Critical=临界成品 Order=订单成品）
    /// </summary>
    public FinishedProductType ProductType { get; set; }

    /// <summary>
    /// 采购支数（定尺时必填）
    /// </summary>
    public int? RequiredPiece { get; set; }

    /// <summary>
    /// 采购重量(kg)
    /// </summary>
    public decimal RequiredWeight { get; set; }

    /// <summary>
    /// 投料倍率(1制几)
    /// </summary>
    public int? InputMultiple { get; set; }

    /// <summary>
    /// 要求到货日期
    /// </summary>
    public DateTime? RequiredDate { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    // ========== 工单冗余字段（默认与工单一致，可编辑） ==========

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 规格（外径*壁厚）
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 外径负公差(mm)
    /// </summary>
    public decimal OuterDiameterNegative { get; set; }

    /// <summary>
    /// 外径正公差(mm)
    /// </summary>
    public decimal OuterDiameterPositive { get; set; }

    /// <summary>
    /// 壁厚负公差(mm)
    /// </summary>
    public decimal WallThicknessNegative { get; set; }

    /// <summary>
    /// 壁厚正公差(mm)
    /// </summary>
    public decimal WallThicknessPositive { get; set; }

    /// <summary>
    /// 长度状态
    /// </summary>
    public LengthStatus LengthStatus { get; set; }

    /// <summary>
    /// 最小长度(mm)
    /// </summary>
    public decimal? MinLength { get; set; }

    /// <summary>
    /// 最大长度(mm)
    /// </summary>
    public decimal? MaxLength { get; set; }

    /// <summary>
    /// 交货状态
    /// </summary>
    public DeliveryState DeliveryState { get; set; }

    /// <summary>
    /// 工艺周期（天）：成品采购默认为3天
    /// </summary>
    public int StandardCycle { get; set; }
}
