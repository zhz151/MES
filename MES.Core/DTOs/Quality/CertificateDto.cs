using MES.Core.Enums;

namespace MES.Core.DTOs.Quality;

/// <summary>
/// 质保书列表 DTO
/// </summary>
public class CertificateDto
{
    public int Id { get; set; }
    public string CertificateNo { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public string? CustomerName { get; set; }
    public string? ProductStandard { get; set; }
    public string? ProductName { get; set; }
    public DeliveryState? DeliveryStatus { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime UpdatedTime { get; set; }
    public string? UpdatedBy { get; set; }
}
