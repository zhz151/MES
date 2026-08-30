// 文件路径: MES.Api/Services/HangfireJobService.cs

using MES.Data;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.Quality;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Interfaces.WorkOrder;
using MES.Services.Scheduling;
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

    /// <summary>
    /// 全项目数据更新定时任务（中午 11:55 / 晚上 23:55 各一次）
    /// 兜底重建所有物化读模型 + 失效派生缓存，修复增量刷新漏网/异常数据导致的不同步。
    /// 执行顺序关键：WorkOrderExecutionSummary 先刷（数据源最广）→ OrderListSummary/WorkOrderListSummary（依赖执行读模型聚合）→ QualityProcessTracking（独立）。
    /// </summary>
    public async Task FullProjectDataRefreshJob()
    {
        _logger.LogInformation("========== 开始全项目数据更新定时任务 ==========");

        try
        {
            // 每步独立 scope（独立 DbContext），隔离跟踪状态，避免连续刷新不同读模型共享 context 的实体跟踪冲突
            // 1. 工单执行状况读模型全量重建（WorkOrderExecutionSummary，其他读模型的数据源）
            await RunStepAsync("工单执行状况全量刷新",
                sp => sp.GetRequiredService<IWorkOrderExecutionService>().RefreshAllAsync());

            // 2. 订单列表读模型全量重建（OrderListSummary，从执行读模型聚合 ScheduleStage/UrgencyLevel 等）
            await RunStepAsync("订单列表全量刷新",
                sp => sp.GetRequiredService<IOrderService>().RefreshAllAsync());

            // 3. 用料计划总览读模型全量重建（WorkOrderListSummary，依赖批次有效产出 + 执行读模型排程档位）
            await RunStepAsync("用料计划总览全量刷新",
                sp => sp.GetRequiredService<IWorkOrderListSummaryRefreshService>().RefreshAllAsync());

            // 4. 质量过程跟踪物化表全量重建（QualityProcessTracking）
            await RunStepAsync("质量过程跟踪全量刷新",
                sp => sp.GetRequiredService<IQualityProcessTrackingService>().RefreshAllAsync());

            // 5. 失效派生缓存：待发货 C0/C1/C2 + 冷轧排程三缓存键（MachineEstimate/ScheduleSuggestion/MachineGroup）
            await RunStepAsync("派生缓存失效", async sp =>
            {
                await sp.GetRequiredService<IPendingDeliveryQueryService>().InvalidateCachesAsync();

                var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                cache.Remove(ColdRollPlanService.MachineEstimateCacheKey);
                cache.Remove(ColdRollPlanService.ScheduleSuggestionCacheKey);
                cache.Remove(ColdRollPlanService.MachineGroupCacheKey);
            });

            _logger.LogInformation("========== 全项目数据更新定时任务完成 ==========");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "全项目数据更新定时任务失败");
            throw;
        }
    }

    /// <summary>执行单个刷新步骤（独立 scope）并记录成功/失败日志（单步失败不中断后续步骤）</summary>
    private async Task RunStepAsync(string stepName, Func<IServiceProvider, Task> action)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            await action(scope.ServiceProvider);
            _logger.LogInformation("全项目数据更新步骤成功: {Step}", stepName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "全项目数据更新步骤失败: {Step}", stepName);
        }
    }
}