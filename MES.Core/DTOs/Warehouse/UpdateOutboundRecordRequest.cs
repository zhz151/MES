using MES.Core.Enums;

namespace MES.Core.DTOs.Warehouse;

/// <summary>
/// 更新出库记录请求
/// </summary>
public class UpdateOutboundRecordRequest
{
    public OutboundType? OutboundType { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? SourceOrderNo { get; set; }
    public string? TargetCompany { get; set; }
    public int? OutboundQuantity { get; set; }
    public decimal? OutboundWeight { get; set; }
    public decimal? OutboundMeters { get; set; }
    public DateTime? OutboundDate { get; set; }
    public string? Remark { get; set; }
}
