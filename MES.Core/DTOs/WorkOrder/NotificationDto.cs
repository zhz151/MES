namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 统一通知DTO
/// </summary>
public class NotificationDto
{
    public int Id { get; set; }
    public string NotificationType { get; set; } = null!;
    public int? TargetId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
}
