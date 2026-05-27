using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;

namespace MES.Services;

/// <summary>
/// 通知服务实现（统一使用 Notifications 表）
/// </summary>
public class NotificationService : INotificationService
{
    private readonly AppDbContext _context;

    public NotificationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetUnreadCountAsync()
    {
        return await _context.Notifications
            .Where(n => !n.IsRead)
            .CountAsync();
    }

    public async Task<PagedResult<NotificationDto>> GetPagedNotificationsAsync(int pageIndex, int pageSize)
    {
        var query = _context.Notifications
            .OrderByDescending(n => n.CreatedTime);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                NotificationType = n.NotificationType,
                TargetId = n.TargetId,
                Title = n.Title,
                Content = n.Content,
                IsRead = n.IsRead,
                CreatedTime = n.CreatedTime
            })
            .ToListAsync();

        return new PagedResult<NotificationDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public async Task MarkAsReadAsync(int id)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id);
        if (notification != null && !notification.IsRead)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync()
    {
        var unread = await _context.Notifications
            .Where(n => !n.IsRead)
            .ToListAsync();
        foreach (var n in unread)
        {
            n.IsRead = true;
        }
        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasRecentItemChangedNotificationAsync(string orderNumber, int minutes)
    {
        var cutoff = DateTimeOffset.Now.AddMinutes(-minutes);
        return await _context.Notifications
            .AnyAsync(n => n.NotificationType == "OrderChanged" &&
                           n.Content != null &&
                           n.Content.Contains(orderNumber) &&
                           !n.IsRead &&
                           n.CreatedTime >= cutoff);
    }

    public async Task<List<NotificationDto>> GetUnreadByTypeAsync(string notificationType)
    {
        return await _context.Notifications
            .Where(n => n.NotificationType == notificationType && !n.IsRead)
            .OrderByDescending(n => n.CreatedTime)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                NotificationType = n.NotificationType,
                TargetId = n.TargetId,
                Title = n.Title,
                Content = n.Content,
                IsRead = n.IsRead,
                CreatedTime = n.CreatedTime
            })
            .ToListAsync();
    }

    public async Task MarkAllByTypeAsReadAsync(string notificationType)
    {
        var unread = await _context.Notifications
            .Where(n => n.NotificationType == notificationType && !n.IsRead)
            .ToListAsync();
        foreach (var n in unread)
            n.IsRead = true;
        await _context.SaveChangesAsync();
    }
}
