// 文件路径: MES.Api/Utils/HangfireCustomBasicAuthorizationFilter.cs

using Hangfire.Dashboard;

namespace MES.Api.Utils;

/// <summary>
/// Hangfire 面板授权过滤器
/// </summary>
public class HangfireCustomBasicAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // 开发环境直接允许
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            return true;
        }

        // 生产环境需要 Basic 认证
        string? authHeader = httpContext.Request.Headers["Authorization"];
        if (authHeader != null && authHeader.StartsWith("Basic "))
        {
            var encodedUsernamePassword = authHeader["Basic ".Length..].Trim();
            var decodedUsernamePassword = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(encodedUsernamePassword));

            var parts = decodedUsernamePassword.Split(':', 2);
            var username = parts[0];
            var password = parts.Length > 1 ? parts[1] : string.Empty;

            var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
            var expectedUsername = config["Hangfire:Username"] ?? "admin";
            var expectedPassword = config["Hangfire:Password"] ?? "hangfire123";

            return username == expectedUsername && password == expectedPassword;
        }

        return false;
    }
}