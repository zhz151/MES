using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using MES.Core.Interfaces.Infrastructure;

namespace MES.Api.Services;

/// <summary>
/// 当前用户服务，从 HTTP 上下文获取用户名（仅 Api 层依赖 IHttpContextAccessor）
/// </summary>
public class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetUserName()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.Name is { } name && !string.IsNullOrEmpty(name))
            return name;

        var emailClaim = httpContext?.User?.FindFirst(ClaimTypes.Email);
        if (emailClaim != null)
            return emailClaim.Value;

        return "system";
    }
}
