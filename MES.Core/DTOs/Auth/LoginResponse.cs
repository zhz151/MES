namespace MES.Core.DTOs.Auth;

/// <summary>
/// 登录响应DTO
/// </summary>
public class LoginResponse
{
    /// <summary>
    /// JWT令牌
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 邮箱
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 用户名
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 角色列表
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime Expires { get; set; }

    /// <summary>
    /// 用户全名
    /// </summary>
    public string FullName { get; set; } = string.Empty;
}