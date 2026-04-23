using System.Collections.Generic;
using System.Threading.Tasks;
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

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
    Task<PagedResult<OrderChangeNotificationDto>> GetPagedNotificationsAsync(int pageIndex, int pageSize);

    /// <summary>
    /// 标记单条通知为已读
    /// </summary>
    Task MarkAsReadAsync(int id);

    /// <summary>
    /// 标记所有通知为已读
    /// </summary>
    Task MarkAllAsReadAsync();

    /// <summary>
    /// 检查是否存在最近（N分钟内）的未读项次变更通知（用于去重）
    /// </summary>
    Task<bool> HasRecentItemChangedNotificationAsync(string orderNumber, int minutes);
}