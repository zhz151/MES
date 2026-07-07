namespace MES.Core.DTOs;

/// <summary>
/// 出库记录 DTO
/// </summary>
public class OutboundRecordDto
{
    public long Id { get; set; }
    public int InventoryBatchId { get; set; }
    public string? BatchNo { get; set; }
    public string OutboundType { get; set; } = string.Empty;
    public string? SourceOrderNo { get; set; }
    public string? TargetCompany { get; set; }
    public int OutboundQuantity { get; set; }
    public decimal OutboundWeight { get; set; }
    public DateTime OutboundDate { get; set; }
    public string? Remark { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
}
