using MES.Core.Enums;

namespace MES.Data.Entities;

/// <summary>
/// 自有料使用计划（库存直接使用）
/// </summary>
public class InventoryPlan : BaseEntity
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
    /// 关联库存批次号（字符串，无FK）
    /// </summary>
    public string InventoryBatchNo { get; set; } = null!;

    /// <summary>
    /// 批次号（冗余展示）
    /// </summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>
    /// 物料名称（从库存批次冗余）
    /// </summary>
    public string MaterialType { get; set; } = null!;

    /// <summary>
    /// 工厂牌号（冗余展示）
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 名义规格（冗余展示）
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 放置区域（从库存批次冗余）
    /// </summary>
    public string? LocationArea { get; set; }

    /// <summary>
    /// 放置架号（从库存批次冗余）
    /// </summary>
    public string? LocationRack { get; set; }

    /// <summary>
    /// 投料倍率（1支原料做几支成品）
    /// </summary>
    public int InputMultiple { get; set; } = 1;

    /// <summary>
    /// 使用模式：All=全部使用 Partial=部分使用
    /// </summary>
    public string UsageMode { get; set; } = "All";

    /// <summary>
    /// 出库支数（部分使用时填写，全用时取批次剩余量）
    /// </summary>
    public int? UsedQuantity { get; set; }

    /// <summary>
    /// 出库重量(kg)
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
    /// 改制类型：null=普通库存使用, EmptyDrawing=空拉改制, FewerPass=少道次改制, ManualSelect=人工选择改制
    /// </summary>
    public ReworkType? ReworkType { get; set; }

    /// <summary>
    /// 工艺周期（天）：库存使用默认为3天，库料改制根据标准工艺生产周期计算
    /// </summary>
    public int StandardCycle { get; set; }
}
