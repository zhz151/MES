using MES.Core.Enums;

namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 在产主工单计划 DTO
/// </summary>
public class InMainWorkOrderPlanDto
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public DateTime PlanDate { get; set; }
    public int ProductionBatchId { get; set; }
    public string BatchNo { get; set; } = null!;
    public string MainWorkOrderNo { get; set; } = null!;
    public decimal AllocatedWeight { get; set; }
    public int? AllocatedQuantity { get; set; }
    public int ProductionRatio { get; set; }
    public DateTime? RequiredDate { get; set; }
    public InventoryPlanStatus PlanStatus { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 创建在产主工单计划请求
/// </summary>
public class CreateInMainWorkOrderPlanRequest
{
    public int WorkOrderId { get; set; }
    public DateTime PlanDate { get; set; }
    public int ProductionBatchId { get; set; }
    public int? AllocatedQuantity { get; set; }
    public decimal AllocatedWeight { get; set; }
    public int ProductionRatio { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 可用主工单批次 DTO（展示给用户选择）
/// </summary>
public class AvailableMainWorkOrderBatchDto
{
    /// <summary>批次ID</summary>
    public int Id { get; set; }
    /// <summary>生产编号</summary>
    public string BatchNo { get; set; } = null!;
    /// <summary>挂牌号</summary>
    public string? TagNo { get; set; }
    /// <summary>工单号（批次关联工单号）</summary>
    public string WorkOrderNo { get; set; } = null!;
    /// <summary>工厂牌号</summary>
    public string PlantGrade { get; set; } = null!;
    /// <summary>成品规格</summary>
    public string Specification { get; set; } = null!;
    /// <summary>长度状态</summary>
    public LengthStatus? LengthStatus { get; set; }
    /// <summary>最大长度(mm)</summary>
    public decimal? MaxLength { get; set; }
    /// <summary>批次状态</summary>
    public BatchStatus? Status { get; set; }
    /// <summary>投成倍数（制成倍数）</summary>
    public int ProductionRatio { get; set; }
    /// <summary>有效原料支数</summary>
    public int? CurrentValidQty { get; set; }
    /// <summary>有效原料重量(kg)</summary>
    public int? CurrentValidWeight { get; set; }
    /// <summary>原主工单号总重量(kg)（主号级需求，来自工单执行摘要聚合）</summary>
    public decimal MainTotalWeight { get; set; }

    /// <summary>
    /// 已被其他未取消的在产主工单计划预留的支数（含已投料，跨工单累计）
    /// </summary>
    public int ReservedQuantity { get; set; }

    /// <summary>
    /// 已被其他未取消的在产主工单计划预留的重量(kg)（含已投料，跨工单累计）
    /// </summary>
    public decimal ReservedWeight { get; set; }

    /// <summary>总有效投料重量(kg)：按原主工单号聚合本页所有可用批次的 CurrentValidWeight 之和</summary>
    public decimal MainNoTotalValidWeight { get; set; }

    /// <summary>总预留重量(kg)：按原主工单号聚合本页所有可用批次的 ReservedWeight 之和</summary>
    public decimal MainNoTotalReservedWeight { get; set; }

    /// <summary>
    /// 可分配剩余总重量(kg) = max(0, 总有效投料重量 − 原主工单号总重量 − 总预留重量)。
    /// 主工单富余为负时归 0（批次仍呈现，校验拦截）。
    /// </summary>
    public decimal MainNoAllocatableRemaining { get; set; }
}
