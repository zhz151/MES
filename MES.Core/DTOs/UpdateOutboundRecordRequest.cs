namespace MES.Core.DTOs;

/// <summary>
/// 更新出库记录请求
/// </summary>
public class UpdateOutboundRecordRequest
{
    public string? OutboundType { get; set; }
    public string? SourceOrderNo { get; set; }
    public string? TargetCompany { get; set; }
    public int? OutboundQuantity { get; set; }
    public decimal? OutboundWeight { get; set; }
    public DateTime? OutboundDate { get; set; }
    public string? Remark { get; set; }
}
