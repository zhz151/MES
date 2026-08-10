using MES.Core.Enums;
using MES.Core.Helpers;
namespace MES.Core.DTOs.Warehouse;

/// <summary>
/// 出库记录 DTO
/// </summary>
public class OutboundRecordDto
{
    public long Id { get; set; }
    public int InventoryBatchId { get; set; }
    public string? BatchNo { get; set; }
    public OutboundType OutboundType { get; set; }
    public string OutboundTypeDisplay => EnumHelper.GetDisplayName(OutboundType);
    public string? WorkOrderNo { get; set; }
    public string? SourceOrderNo { get; set; }
    public string? TargetCompany { get; set; }
    public int OutboundQuantity { get; set; }
    public decimal OutboundWeight { get; set; }
    public decimal? OutboundMeters { get; set; }
    public DateTime OutboundDate { get; set; }
    public string? Remark { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
}
