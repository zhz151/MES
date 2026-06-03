using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MES.Data;

/// <summary>
/// 设计时 DbContext 工厂（用于 dotnet ef migrations 命令）
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // 优先从环境变量读取，用于 CI/CD 场景
        var connectionString = Environment.GetEnvironmentVariable("MES_CONNECTION_STRING");

        if (string.IsNullOrEmpty(connectionString))
        {
            // 从 appsettings.json 读取
            var apiDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "MES.Api");
            var configPaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
                Path.Combine(apiDir, "appsettings.json"),
                // appsettings.Development.json 在 .gitignore 中，可存放真实连接串
                Path.Combine(apiDir, "appsettings.Development.json"),
            };

            foreach (var configPath in configPaths)
            {
                if (!File.Exists(configPath)) continue;
                var json = File.ReadAllText(configPath);
                var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("ConnectionStrings", out var csNode)
                    && csNode.TryGetProperty("Default", out var connNode))
                {
                    connectionString = connNode.GetString();
                    if (!string.IsNullOrEmpty(connectionString)) break;
                }
            }
        }

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "未配置数据库连接字符串。请通过环境变量 MES_CONNECTION_STRING 或在 appsettings.json 的 ConnectionStrings:Default 中配置。");
        }

        optionsBuilder.UseSqlServer(connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }
}
