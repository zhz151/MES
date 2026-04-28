using MES.Core.Enums;

namespace MES.Data.Entities;

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
    /// 要求到货日期
    /// </summary>
    public DateTime? RequiredDate { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
