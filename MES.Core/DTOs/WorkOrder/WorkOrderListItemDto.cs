using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 工单列表项 DTO（精简版，仅含 WorkOrder 实体字段，不含用料计划聚合数据）
/// </summary>
public class WorkOrderListItemDto
{
    public int Id { get; set; }
    public string WorkOrderNo { get; set; } = null!;
    public string SalesOrderNo { get; set; } = null!;
    public string ProductionMainNo { get; set; } = null!;
    public string? ProductionSubNo { get; set; }
    public DateTime SignDate { get; set; }
    public string Salesman { get; set; } = null!;
    public string? EndCustomer { get; set; }
    public DateTime DeliveryDate { get; set; }
    public bool DelayPenalty { get; set; }
    public SettlementMethod SettlementMethod { get; set; }
    public string SettlementMethodDisplay => EnumHelper.GetDisplayName(SettlementMethod);
    public string PlantGrade { get; set; } = null!;
    public PipeManufacturingType PipeManufacturingType { get; set; }
    public string PipeManufacturingTypeDisplay => EnumHelper.GetDisplayName(PipeManufacturingType);
    public string Specification { get; set; } = null!;
    public LengthStatus LengthStatus { get; set; }
    public string LengthStatusDisplay => EnumHelper.GetDisplayName(LengthStatus);
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalWeight { get; set; }
    public DeliveryState DeliveryState { get; set; }
    public string DeliveryStateDisplay => EnumHelper.GetDisplayName(DeliveryState);
    public int TotalItemCount { get; set; }
    public WorkOrderStatus Status { get; set; }
    public string StatusDisplay => EnumHelper.GetDisplayName(Status);
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedTime { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedTime { get; set; }
}
