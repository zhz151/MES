namespace MES.Core.DTOs;

/// <summary>
/// 生产批次列表DTO
/// </summary>
public class ProductionBatchListDto
{
    public int Id { get; set; }
    public string BatchNo { get; set; } = null!;
    public string? TagNo { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
    public string WorkOrderNo { get; set; } = null!;
    public string SalesOrderNo { get; set; } = null!;
    public string ProductionMainNo { get; set; } = null!;
    public string? ProductionSubNo { get; set; }
    public string? ProductionType { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? CurrentExecDate { get; set; }
    public string? CurrentGroupName { get; set; }
    public string? CurrentSectionName { get; set; }
    public string? CurrentEquipmentName { get; set; }
    public string? CurrentOutsource { get; set; }
    public string? CurrentSpec { get; set; }
    public string? NextSectionName { get; set; }
    public string? CorrespondingSpec { get; set; }
    public string CreatedBy { get; set; } = null!;
}
