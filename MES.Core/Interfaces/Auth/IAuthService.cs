using MES.Core.DTOs.Auth;
using MES.Core.Models;

namespace MES.Core.Interfaces.Auth;

public interface IAuthService
{
    Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<object>> LogoutAsync();
    Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string refreshToken);
    Task<ApiResponse<UserInfoResponse>> GetCurrentUserAsync();
}
