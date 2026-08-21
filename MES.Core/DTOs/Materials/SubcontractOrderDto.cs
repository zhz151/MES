using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Materials;

public class SubcontractOrderDto
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = null!;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public string ProcessType { get; set; } = "Piercing";
    public string ProcessTypeDisplay => "穿孔";
    public SubcontractOrderStatus Status { get; set; }
    public string StatusDisplay => EnumHelper.GetDisplayName(Status);
    public bool IsForceCompleted { get; set; }
    public string? FurnaceNumber { get; set; }
    public MaterialType OutMaterialCategory { get; set; }
    public string OutMaterialCategoryDisplay => EnumHelper.GetDisplayName(OutMaterialCategory);
    public string OutPlantGrade { get; set; } = null!;
    public string OutSpecification { get; set; } = null!;
    public int OutQuantity { get; set; }
    public decimal OutWeight { get; set; }
    /// <summary>实发支数（仓库实际出库汇总）</summary>
    public int? ActualOutboundQuantity { get; set; }
    /// <summary>实发重量（仓库实际出库汇总）</summary>
    public decimal? ActualOutboundWeight { get; set; }
    public DateTime? ReturnDeadline { get; set; }
    public int? InQuantity { get; set; }
    public decimal? InWeight { get; set; }
    /// <summary>退货量（支）：退货出库归集到委外单号级</summary>
    public int ReturnQuantity { get; set; }
    /// <summary>退货重量（kg）：退货出库归集到委外单号级</summary>
    public decimal ReturnWeight { get; set; }
    public string? Remark { get; set; }
    public List<SubcontractReturnItemDto> ReturnItems { get; set; } = new();
    public DateTimeOffset CreatedTime { get; set; }

    // ========== 工单来源字段（从 ReturnItems 首个 SourceWorkOrderNo 关联 WorkOrder 查询） ==========
    public string? SourceWorkOrderNo { get; set; }
    public string? WoSalesOrderNo { get; set; }
    public string? WoProductionMainNo { get; set; }
    public string? WoProductionSubNo { get; set; }
    public DateTime? WoSignDate { get; set; }
    public string? WoSalesman { get; set; }
    public string? WoEndCustomer { get; set; }
    public DateTime? WoDeliveryDate { get; set; }
    public bool WoDelayPenalty { get; set; }
    public SettlementMethod? WoSettlementMethod { get; set; }
    public string? WoSettlementMethodDisplay => WoSettlementMethod.HasValue ? EnumHelper.GetDisplayName(WoSettlementMethod.Value) : null;
    public string? WoPlantGrade { get; set; }
    public string? WoSpecification { get; set; }
    public LengthStatus? WoLengthStatus { get; set; }
    public string? WoLengthStatusDisplay => WoLengthStatus.HasValue ? EnumHelper.GetDisplayName(WoLengthStatus.Value) : null;
    public decimal? WoMaxLength { get; set; }
    public int? WoTotalQuantity { get; set; }
    public decimal? WoTotalWeight { get; set; }
    public DeliveryState? WoDeliveryState { get; set; }
    public string? WoDeliveryStateDisplay => WoDeliveryState.HasValue ? EnumHelper.GetDisplayName(WoDeliveryState.Value) : null;
    public int? WoTotalItemCount { get; set; }
}

public class SubcontractReturnItemDto
{
    public int Id { get; set; }
    public int SubcontractOrderId { get; set; }
    public int Sequence { get; set; }
    public MaterialType MaterialCategory { get; set; }
    public string MaterialCategoryDisplay => EnumHelper.GetDisplayName(MaterialCategory);
    public string? PlantGrade { get; set; }
    public string ProcessSpecification { get; set; } = null!;
    public decimal? UnitWeight { get; set; }
    public int? RequiredQuantity { get; set; }
    public decimal? RequiredWeight { get; set; }
    public int? InputMultiple { get; set; }
    public string? ProcessStatusRemark { get; set; }
    public string? Remark { get; set; }
    public decimal? ProcessUnitPrice { get; set; }
    public decimal? ProcessTotalAmount { get; set; }
    public string? SourceWorkOrderNo { get; set; }

    // ========== 回收执行数据 ==========
    public int ReturnedQuantity { get; set; }
    public decimal ReturnedWeight { get; set; }
    /// <summary>退货量（支）：退货出库归集到序号级</summary>
    public int ReturnQuantity { get; set; }
    /// <summary>退货重量（kg）：退货出库归集到序号级</summary>
    public decimal ReturnWeight { get; set; }
    public SubcontractOrderStatus ProcessStatus { get; set; }
    public string ProcessStatusDisplay => EnumHelper.GetDisplayName(ProcessStatus);
    public bool IsForceCompleted { get; set; }

    // ========== 工单来源字段（按每个SourceWorkOrderNo各自关联） ==========
    public string? WoSalesOrderNo { get; set; }
    public string? WoProductionMainNo { get; set; }
    public string? WoProductionSubNo { get; set; }
    public DateTime? WoSignDate { get; set; }
    public string? WoSalesman { get; set; }
    public string? WoEndCustomer { get; set; }
    public DateTime? WoDeliveryDate { get; set; }
    public bool WoDelayPenalty { get; set; }
    public SettlementMethod? WoSettlementMethod { get; set; }
    public string? WoSettlementMethodDisplay => WoSettlementMethod.HasValue ? EnumHelper.GetDisplayName(WoSettlementMethod.Value) : null;
    public string? WoPlantGrade { get; set; }
    public string? WoSpecification { get; set; }
    public LengthStatus? WoLengthStatus { get; set; }
    public string? WoLengthStatusDisplay => WoLengthStatus.HasValue ? EnumHelper.GetDisplayName(WoLengthStatus.Value) : null;
    public decimal? WoMaxLength { get; set; }
    public int? WoTotalQuantity { get; set; }
    public decimal? WoTotalWeight { get; set; }
    public DeliveryState? WoDeliveryState { get; set; }
    public string? WoDeliveryStateDisplay => WoDeliveryState.HasValue ? EnumHelper.GetDisplayName(WoDeliveryState.Value) : null;
    public int? WoTotalItemCount { get; set; }
}

public class CreateSubcontractOrderRequest
{
    public int SupplierId { get; set; }
    public DateTime OrderDate { get; set; }
    public string ProcessType { get; set; } = "Piercing";
    public string? FurnaceNumber { get; set; }
    public MaterialType OutMaterialCategory { get; set; }
    public string OutPlantGrade { get; set; } = null!;
    public string OutSpecification { get; set; } = null!;
    public int OutQuantity { get; set; }
    public decimal OutWeight { get; set; }
    public DateTime? ReturnDeadline { get; set; }
    public string? Remark { get; set; }
    public List<CreateReturnItemRequest> ReturnItems { get; set; } = new();
}

public class CreateReturnItemRequest
{
    public MaterialType MaterialCategory { get; set; }
    public string? PlantGrade { get; set; }
    public string ProcessSpecification { get; set; } = null!;
    public decimal? UnitWeight { get; set; }
    public int? RequiredQuantity { get; set; }
    public decimal? RequiredWeight { get; set; }
    public int? InputMultiple { get; set; }
    public string? ProcessStatusRemark { get; set; }
    public string? Remark { get; set; }
    public decimal? ProcessUnitPrice { get; set; }
    public decimal? ProcessTotalAmount { get; set; }
    public string? SourceWorkOrderNo { get; set; }
    public bool IsForceCompleted { get; set; }
}

public class UpdateSubcontractOrderRequest
{
    public int SupplierId { get; set; }
    public string ProcessType { get; set; } = "Piercing";
    public string? FurnaceNumber { get; set; }
    public MaterialType OutMaterialCategory { get; set; }
    public string OutPlantGrade { get; set; } = null!;
    public string OutSpecification { get; set; } = null!;
    public int OutQuantity { get; set; }
    public decimal OutWeight { get; set; }
    public DateTime? ReturnDeadline { get; set; }
    public string? Remark { get; set; }
    public List<CreateReturnItemRequest> ReturnItems { get; set; } = new();
}
