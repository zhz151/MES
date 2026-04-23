// 文件路径: MES.Api/Services/HangfireJobService.cs

using MES.Core.Interfaces;

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
}