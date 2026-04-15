using MES.Core.Models;
using MES.Core.DTOs.Auth;

namespace MES.Auth.Services;

/// <summary>
/// 认证服务接口
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="request">登录请求</param>
    /// <returns>登录响应</returns>
    Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request);

    /// <summary>
    /// 注销登录
    /// </summary>
    /// <returns>注销结果</returns>
    Task<ApiResponse<object>> LogoutAsync();

    /// <summary>
    /// 刷新令牌
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <returns>新的令牌响应</returns>
    Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// 获取当前用户信息
    /// </summary>
    /// <returns>用户信息</returns>
    Task<ApiResponse<UserInfoResponse>> GetCurrentUserAsync();
}

/// <summary>
/// JWT令牌服务接口
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// 生成JWT令牌
    /// </summary>
    /// <param name="user">用户信息</param>
    /// <param name="roles">角色列表</param>
    /// <returns>JWT令牌</returns>
    Task<string> GenerateTokenAsync(AppUser user, IList<string> roles);

    /// <summary>
    /// 从令牌中获取用户名
    /// </summary>
    /// <param name="token">JWT令牌</param>
    /// <returns>用户名</returns>
    string GetUsernameFromToken(string token);

    /// <summary>
    /// 验证令牌有效性
    /// </summary>
    /// <param name="token">JWT令牌</param>
    /// <returns>是否有效</returns>
    bool ValidateToken(string token);
}

/// <summary>
/// 登录请求DTO
/// </summary>
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// 用户信息响应DTO
/// </summary>
public class UserInfoResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}