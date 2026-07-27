using MES.Core.Enums;

namespace MES.Data.Entities.WorkOrder;

/// <summary>
/// 在产主工单计划 — 将分工单的用料需求合入主工单的批次中，从主工单的余量中分配
/// </summary>
public class InMainWorkOrderPlan : BaseEntity
{
    /// <summary>
    /// 被覆盖的工单ID（分工单）
    /// </summary>
    public int WorkOrderId { get; set; }

    /// <summary>
    /// 计划日期
    /// </summary>
    public DateTime PlanDate { get; set; }

    /// <summary>
    /// 覆盖它的生产批次ID（主工单所属批次）
    /// </summary>
    public int ProductionBatchId { get; set; }

    /// <summary>
    /// 批次号（从ProductionBatch冗余）
    /// </summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>
    /// 主工单号
    /// </summary>
    public string MainWorkOrderNo { get; set; } = null!;

    /// <summary>
    /// 分配重量(kg)
    /// </summary>
    public decimal AllocatedWeight { get; set; }

    /// <summary>
    /// 分配支数
    /// </summary>
    public int? AllocatedQuantity { get; set; }

    /// <summary>
    /// 制成倍数
    /// </summary>
    public int ProductionRatio { get; set; }

    /// <summary>
    /// 工艺周期（天）（投料满足时继承主工单，未投满时取默认值）
    /// </summary>
    public int StandardCycle { get; set; }

    /// <summary>
    /// 要求到位日期
    /// </summary>
    public DateTime? RequiredDate { get; set; }

    /// <summary>
    /// 计划状态
    /// </summary>
    public InventoryPlanStatus PlanStatus { get; set; } = InventoryPlanStatus.Planned;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
