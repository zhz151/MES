using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services;

/// <summary>
/// 通知服务实现
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
        // 通知使用物理删除，不需要 IsDeleted 条件
        return await _context.OrderChangeNotifications
            .Where(n => !n.IsRead)
            .CountAsync();
    }

    public async Task<PagedResult<OrderChangeNotificationDto>> GetPagedNotificationsAsync(int pageIndex, int pageSize)
    {
        var query = _context.OrderChangeNotifications
            .OrderByDescending(n => n.CreatedTime);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new OrderChangeNotificationDto
            {
                Id = n.Id,
                OrderNumber = n.OrderNumber,
                ChangeType = n.ChangeType,
                WorkOrderCount = n.WorkOrderCount,
                IsRead = n.IsRead,
                CreatedTime = n.CreatedTime
            })
            .ToListAsync();

        return new PagedResult<OrderChangeNotificationDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public async Task MarkAsReadAsync(int id)
    {
        var notification = await _context.OrderChangeNotifications
            .FirstOrDefaultAsync(n => n.Id == id);
        if (notification != null && !notification.IsRead)
        {
            notification.IsRead = true;
            notification.UpdatedTime = DateTimeOffset.Now;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync()
    {
        var unreadNotifications = await _context.OrderChangeNotifications
            .Where(n => !n.IsRead)
            .ToListAsync();
        foreach (var n in unreadNotifications)
        {
            n.IsRead = true;
            n.UpdatedTime = DateTimeOffset.Now;
        }
        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasRecentItemChangedNotificationAsync(string orderNumber, int minutes)
    {
        var cutoff = DateTimeOffset.Now.AddMinutes(-minutes);
        return await _context.OrderChangeNotifications
            .AnyAsync(n => n.OrderNumber == orderNumber &&
                           n.ChangeType == NotificationChangeType.ItemChanged &&
                           !n.IsRead &&
                           n.CreatedTime >= cutoff);
    }
}