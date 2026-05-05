namespace MES.Data.Entities;

/// <summary>
/// 通知记录（不使用BaseEntity，设计上无审计字段）
/// </summary>
public class Notification
{
    /// <summary>
    /// 主键，自增
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 通知类型
    /// </summary>
    public string NotificationType { get; set; } = null!;

    /// <summary>
    /// 关联ID
    /// </summary>
    public int? TargetId { get; set; }

    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// 内容
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// 是否已读
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// 接收人
    /// </summary>
    public string Receiver { get; set; } = null!;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedTime { get; set; }
}
