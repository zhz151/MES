using System.Collections.Generic;
using System.Threading.Tasks;
using MES.Core.Models;

using MES.Core.DTOs.WorkOrder;
namespace MES.Core.Interfaces.WorkOrder;

/// <summary>
/// 通知服务接口
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 获取未读通知数量
    /// </summary>
    Task<int> GetUnreadCountAsync();

    /// <summary>
    /// 分页获取通知列表
    /// </summary>
    Task<PagedResult<NotificationDto>> GetPagedNotificationsAsync(int pageIndex, int pageSize);

    /// <summary>
    /// 标记单条通知为已读
    /// </summary>
    Task MarkAsReadAsync(int id);

    /// <summary>
    /// 标记所有通知为已读
    /// </summary>
    Task MarkAllAsReadAsync();

    /// <summary>
    /// 创建通知
    /// </summary>
    Task CreateAsync(string notificationType, string title, string content, int? targetId = null, string? receiver = null);

    /// <summary>
    /// 检查是否存在最近（N分钟内）的未读项次变更通知（用于去重）
    /// </summary>
    Task<bool> HasRecentItemChangedNotificationAsync(string orderNumber, int minutes);

    /// <summary>
    /// 获取指定类型的未读通知列表
    /// </summary>
    Task<List<NotificationDto>> GetUnreadByTypeAsync(string notificationType);

    /// <summary>
    /// 标记指定类型的所有通知为已读
    /// </summary>
    Task MarkAllByTypeAsReadAsync(string notificationType);
}
