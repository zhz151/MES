using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Materials;

public class PurchaseOrderDto
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = null!;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public PurchaseOrderStatus Status { get; set; }
    public bool IsForceCompleted { get; set; }
    public MaterialType MaterialCategory { get; set; }
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public decimal? UnitWeight { get; set; }
    public int? Quantity { get; set; }
    public decimal Weight { get; set; }
    public DateTime RequiredDate { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalAmount { get; set; }
    public DateTime? LastArrivalDate { get; set; }
    public int ReceivedQuantity { get; set; }
    public decimal ReceivedWeight { get; set; }
    public int ReturnQuantity { get; set; }
    public decimal ReturnWeight { get; set; }
    public int? InputMultiple { get; set; }
    public string? SourceWorkOrderNo { get; set; }
    public string? Remark { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }

    // ========== 工单来源字段（从 WorkOrder 关联查询） ==========
    public string? WoSalesOrderNo { get; set; }
    public string? WoProductionMainNo { get; set; }
    public string? WoProductionSubNo { get; set; }
    public DateTime? WoSignDate { get; set; }
    public string? WoSalesman { get; set; }
    public string? WoEndCustomer { get; set; }
    public DateTime? WoDeliveryDate { get; set; }
    public bool WoDelayPenalty { get; set; }
    public SettlementMethod? WoSettlementMethod { get; set; }
    public string? WoPlantGrade { get; set; }
    public string? WoSpecification { get; set; }
    public LengthStatus? WoLengthStatus { get; set; }
    public decimal? WoMaxLength { get; set; }
    public int? WoTotalQuantity { get; set; }
    public decimal? WoTotalWeight { get; set; }
    public DeliveryState? WoDeliveryState { get; set; }
    public int? WoTotalItemCount { get; set; }

    // ========== 工单实时关注（从工单执行状况读模型 WorkOrderExecutionSummary 按来源工单号关联，无记录默认 null → 前端 "-"） ==========
    /// <summary>工单关注(0=主号暂停 1=主号完成 2=原料锁定 3=生产执行 4=成品检验)</summary>
    public int? ExecutionScheduleStage { get; set; }

    /// <summary>原锁执行备注（原料锁定原因）</summary>
    public string? ExecutionRawMaterialLockRemark { get; set; }

    /// <summary>计划性（A+急/A急/B顺/C缓/D缓）</summary>
    public string? ExecutionUrgencyLevel { get; set; }

    /// <summary>理论截止投料日</summary>
    public DateTime? ExecutionTheoreticalCutoffDate { get; set; }
}

public class CreatePurchaseOrderRequest
{
    public int SupplierId { get; set; }
    public DateTime OrderDate { get; set; }
    public MaterialType MaterialCategory { get; set; }
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public decimal? UnitWeight { get; set; }
    public int? Quantity { get; set; }
    public decimal Weight { get; set; }
    public DateTime RequiredDate { get; set; }
    public decimal? UnitPrice { get; set; }
    public int? InputMultiple { get; set; }
    public string? SourceWorkOrderNo { get; set; }
    public string? Remark { get; set; }
}

public class UpdatePurchaseOrderRequest
{
    public int SupplierId { get; set; }
    public MaterialType MaterialCategory { get; set; }
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public decimal? UnitWeight { get; set; }
    public int? Quantity { get; set; }
    public decimal Weight { get; set; }
    public DateTime RequiredDate { get; set; }
    public decimal? UnitPrice { get; set; }
    public int? InputMultiple { get; set; }
    public string? SourceWorkOrderNo { get; set; }
    public string? Remark { get; set; }
}

public class UpdateOrderStatusRequest
{
    public bool IsForceCompleted { get; set; }
}
