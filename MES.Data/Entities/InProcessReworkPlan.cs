using MES.Core.Enums;

namespace MES.Data.Entities;

/// <summary>
/// 在产改制计划 — 使用在产/未产的非工单批次进行库料改制
/// </summary>
public class InProcessReworkPlan : BaseEntity
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
    /// 关联生产批次ID
    /// </summary>
    public int ProductionBatchId { get; set; }

    /// <summary>
    /// 批次号（从ProductionBatch冗余）
    /// </summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>
    /// 挂牌号（从ProductionBatch冗余）
    /// </summary>
    public string? BatchTagNo { get; set; }

    /// <summary>
    /// 物料名称（从ProductionBatch冗余）
    /// </summary>
    public string MaterialName { get; set; } = null!;

    /// <summary>
    /// 工厂牌号（从ProductionBatch冗余）
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 名义规格（从ProductionBatch冗余）
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 长度状态（定尺/非尺）
    /// </summary>
    public string LengthStatus { get; set; } = null!;

    /// <summary>
    /// 投料倍率（1支原料做几支成品）
    /// </summary>
    public int InputMultiple { get; set; } = 1;

    /// <summary>
    /// 使用支数
    /// </summary>
    public int? UsedQuantity { get; set; }

    /// <summary>
    /// 使用重量(kg)
    /// </summary>
    public decimal UsedWeight { get; set; }

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

    /// <summary>
    /// 改制类型：EmptyDrawing=空拉改制, FewerPass=少道次改制, ManualSelect=人工选择改制
    /// </summary>
    public ReworkType ReworkType { get; set; }

    /// <summary>
    /// 工艺周期（天）
    /// </summary>
    public int StandardCycle { get; set; }
}
