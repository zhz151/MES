// 文件路径: MES.Api/Services/HangfireJobService.cs

using MES.Core.Interfaces;
using MES.Data;
using Microsoft.EntityFrameworkCore;

namespace MES.Api.Services;

/// <summary>
/// Hangfire 定时任务服务
/// </summary>
public class HangfireJobService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HangfireJobService> _logger;

    public HangfireJobService(IServiceProvider serviceProvider, ILogger<HangfireJobService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// 订单变更检测定时任务
    /// </summary>
    public async Task CheckOrderChangeJob()
    {
        _logger.LogInformation("开始执行订单变更检测定时任务");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var workOrderService = scope.ServiceProvider.GetRequiredService<IWorkOrderService>();
            await workOrderService.CheckAllOrdersChangeAsync();

            _logger.LogInformation("订单变更检测定时任务执行完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "订单变更检测定时任务执行失败");
            throw;
        }
    }

    /// <summary>
    /// 清理过期通知（保留30天）定时任务
    /// </summary>
    public async Task CleanupOldNotificationsJob()
    {
        _logger.LogInformation("开始执行清理过期通知定时任务");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var cutoff = DateTimeOffset.Now.AddDays(-30);
            var oldNotifications = await context.OrderChangeNotifications
                .Where(n => n.CreatedTime < cutoff)
                .ToListAsync();
            var count = oldNotifications.Count;

            if (count > 0)
            {
                context.OrderChangeNotifications.RemoveRange(oldNotifications);
                await context.SaveChangesAsync();
            }

            _logger.LogInformation("清理过期通知完成，共删除 {Count} 条记录", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期通知定时任务执行失败");
            throw;
        }
    }
}