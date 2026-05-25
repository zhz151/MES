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
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            if (!File.Exists(configPath))
            {
                // 尝试从上级 MES.Api 目录查找
                configPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "MES.Api", "appsettings.json");
            }

            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = System.Text.Json.JsonDocument.Parse(json);
                connectionString = config.RootElement
                    .GetProperty("ConnectionStrings")
                    .GetProperty("Default")
                    .GetString();
            }
        }

        if (string.IsNullOrEmpty(connectionString))
        {
            // 回退到本地开发连接字符串
            connectionString = "Server=localhost;Database=MES;Trusted_Connection=true;TrustServerCertificate=true;MultipleActiveResultSets=true";
        }

        optionsBuilder.UseSqlServer(connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }
}
