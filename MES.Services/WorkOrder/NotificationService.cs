using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Interfaces.Equipment;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Interfaces.Materials;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.StandardRegister;
using MES.Core.Interfaces.Quality;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.WorkOrder;

namespace MES.Services.WorkOrder;

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

    public async Task<PagedResult<NotificationDto>> GetPagedNotificationsAsync(int pageIndex, int pageSize)
    {
        var query = _context.Notifications
            .OrderByDescending(n => n.CreatedTime);

        var totalCount = await query.CountAsync();
        var entities = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = entities.Select(n => new NotificationDto
        {
            Id = n.Id,
            NotificationType = Enum.Parse<NotificationType>(n.NotificationType),
            TargetId = n.TargetId,
            Title = n.Title,
            Content = n.Content,
            IsRead = n.IsRead,
            CreatedTime = n.CreatedTime
        }).ToList();

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

    public async Task CreateAsync(string notificationType, string title, string content, int? targetId = null, string? receiver = null)
    {
        _context.Notifications.Add(new Notification
        {
            NotificationType = notificationType,
            TargetId = targetId,
            Title = title,
            Content = content,
            IsRead = false,
            Receiver = receiver ?? string.Empty,
            CreatedTime = DateTimeOffset.Now
        });
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
        var entities = await _context.Notifications
            .Where(n => n.NotificationType == notificationType && !n.IsRead)
            .OrderByDescending(n => n.CreatedTime)
            .ToListAsync();

        return entities.Select(n => new NotificationDto
        {
            Id = n.Id,
            NotificationType = Enum.Parse<NotificationType>(n.NotificationType),
            TargetId = n.TargetId,
            Title = n.Title,
            Content = n.Content,
            IsRead = n.IsRead,
            CreatedTime = n.CreatedTime
        }).ToList();
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
