using MES.Data.Entities;
using MES.Core.DTOs.Auth;
using MES.Core.Models;

namespace MES.Auth.Services;

public interface IAuthService
{
    Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<object>> LogoutAsync();

    Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string refreshToken);

    Task<ApiResponse<UserInfoResponse>> GetCurrentUserAsync();
}
public interface IJwtService
{

    Task<string> GenerateTokenAsync(AppUser user, IList<string> roles);
    string GetUsernameFromToken(string token);

    bool ValidateToken(string token);
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class UserInfoResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}