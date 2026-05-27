using MES.Core.Enums;

namespace MES.Core.DTOs;

/// <summary>
/// 成品采购计划 DTO
/// </summary>
public class PurchaseFinishedPlanDto
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
    /// 成品类型（Critical=临界成品 Order=订单成品）
    /// </summary>
    public string ProductType { get; set; } = null!;

    /// <summary>
    /// 采购支数
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

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public string CreatedBy { get; set; } = null!;

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

    /// <summary>工艺周期（天）</summary>
    public int StandardCycle { get; set; }
}

/// <summary>
/// 创建成品采购计划请求
/// </summary>
public class CreatePurchaseFinishedPlanRequest
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
    /// 成品类型
    /// </summary>
    public string ProductType { get; set; } = null!;

    /// <summary>
    /// 采购支数
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

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 规格
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
    public string LengthStatus { get; set; } = null!;

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
    public string DeliveryState { get; set; } = null!;
}
