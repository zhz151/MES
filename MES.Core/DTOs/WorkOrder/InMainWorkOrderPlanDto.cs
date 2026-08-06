using MES.Core.Enums;
using MES.Core.Helpers;

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
    public int StandardCycle { get; set; }
    public DateTime? RequiredDate { get; set; }
    public InventoryPlanStatus PlanStatus { get; set; }
    public string PlanStatusDisplay => EnumHelper.GetDisplayName(PlanStatus);
    public string PlanStatusText { get; set; } = null!;
    public string? Remark { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public string CreatedBy { get; set; } = null!;
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
    /// <summary>主号总重量(kg)</summary>
    public decimal MainTotalWeight { get; set; }
    /// <summary>主号流转比(%)</summary>
    public decimal MainNoFlowOutputRatio { get; set; }
    /// <summary>可分配上限重量(kg)</summary>
    public decimal AvailableLimit { get; set; }
    /// <summary>用料占比（可分配上限重量 / 分工单总重量）</summary>
    public decimal UsageRatio { get; set; }
}
