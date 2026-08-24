using MES.Core.DTOs.Auth;
using MES.Core.Models;

namespace MES.Core.Interfaces.Auth;

public interface IAuthService
{
    Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string refreshToken);
}
