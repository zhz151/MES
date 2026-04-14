using System.Security.Claims;

namespace MES.Blazor.Extensions;

public static class PermissionExtensions
{
    public static bool HasPermission(this ClaimsPrincipal user, string permission)
    {
        if (user == null || !user.Identity.IsAuthenticated)
            return false;

        return user.Claims.Any(c => 
            c.Type == "Permission" && c.Value.Equals(permission, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasAnyPermission(this ClaimsPrincipal user, params string[] permissions)
    {
        if (user == null || !user.Identity.IsAuthenticated)
            return false;

        return permissions.Any(permission => 
            user.Claims.Any(c => 
                c.Type == "Permission" && c.Value.Equals(permission, StringComparison.OrdinalIgnoreCase)));
    }

    public static bool HasAllPermissions(this ClaimsPrincipal user, params string[] permissions)
    {
        if (user == null || !user.Identity.IsAuthenticated)
            return false;

        return permissions.All(permission => 
            user.Claims.Any(c => 
                c.Type == "Permission" && c.Value.Equals(permission, StringComparison.OrdinalIgnoreCase)));
    }

    public static IEnumerable<string> GetUserPermissions(this ClaimsPrincipal user)
    {
        if (user == null || !user.Identity.IsAuthenticated)
            return Enumerable.Empty<string>();

        return user.Claims
            .Where(c => c.Type == "Permission")
            .Select(c => c.Value)
            .Distinct();
    }

    public static bool IsInContext(this ClaimsPrincipal user, string contextId)
    {
        if (user == null || !user.Identity.IsAuthenticated)
            return false;

        return user.Claims.Any(c => 
            c.Type == "ContextId" && c.Value.Equals(contextId, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasContextPermission(this ClaimsPrincipal user, string contextId, string permission)
    {
        if (user == null || !user.Identity.IsAuthenticated)
            return false;

        var hasContext = user.IsInContext(contextId);
        var hasPermission = user.HasPermission(permission);

        var hasContextPermission = user.Claims.Any(c =>
            c.Type == "ContextPermission" && 
            c.Value.Equals($"{contextId}:{permission}", StringComparison.OrdinalIgnoreCase));

        return hasContext && (hasPermission || hasContextPermission);
    }
}