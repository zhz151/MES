namespace MES.Core.DTOs.Auth;

/// <summary>
/// JWT 令牌生成请求 DTO — 替代直接传递 AppUser 实体
/// </summary>
public class JwtGenerationRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
}
