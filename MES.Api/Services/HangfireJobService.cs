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
            var oldNotifications = await context.Notifications
                .Where(n => n.CreatedTime < cutoff)
                .ToListAsync();
            var count = oldNotifications.Count;

            if (count > 0)
            {
                context.Notifications.RemoveRange(oldNotifications);
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

    /// <summary>
    /// 质量过程跟踪物化表定时刷新（每小时）
    /// 兜底策略：刷新最近1小时有变更的 MRCheck 以及从未刷过的记录
    /// </summary>
    public async Task RefreshQualityProcessTrackingJob()
    {
        _logger.LogInformation("开始执行质量过程跟踪定时刷新");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var qptService = scope.ServiceProvider.GetRequiredService<IQualityProcessTrackingService>();

            var since = DateTimeOffset.UtcNow.AddHours(-1);

            // 查找需要刷新的 MRCheck：1) 最近1小时有更新 或 2) 从未刷新（物化表无对应行）
            var mrCheckIds = await context.MaterialReceiveChecks
                .Where(rc => rc.UpdatedTime >= since
                    || !context.QualityProcessTrackings.Any(q => q.MaterialReceiveCheckId == rc.Id))
                .Select(rc => rc.Id)
                .ToListAsync();

            var successCount = 0;
            foreach (var id in mrCheckIds)
            {
                try
                {
                    await qptService.RefreshByMrCheckIdAsync(id);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "质量过程跟踪单条刷新失败: MRCheckId={MrCheckId}", id);
                }
            }

            _logger.LogInformation("质量过程跟踪定时刷新完成，共处理 {Total} 条，成功 {Success} 条",
                mrCheckIds.Count, successCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "质量过程跟踪定时刷新失败");
            throw;
        }
    }
}