// 文件路径: MES.Api/Health/DatabaseHealthCheck.cs

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MES.Data;

namespace MES.Api.Health;

/// <summary>
/// 数据库连通性探活。
/// <para>
/// 部署脚本与运维轮询 <c>GET /api/health</c> 时使用：只探「能否连上」，
/// 不执行任何业务查询，避免给数据库带来额外压力。
/// </para>
/// <para>
/// <see cref="AppDbContext"/> 为 Scoped，健康检查中间件在请求作用域内解析该项，
/// 因此直接构造函数注入即可，无需 <c>IServiceScopeFactory</c>。
/// </para>
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(AppDbContext dbContext, ILogger<DatabaseHealthCheck> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("数据库连接正常")
                : HealthCheckResult.Unhealthy("数据库无法连接");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "健康检查：数据库探活失败");
            return HealthCheckResult.Unhealthy("数据库探活异常", ex);
        }
    }
}
