// ÎÄ¼þÂ·¾¶: MES.Api/Controllers/AuthController.cs
using Microsoft.AspNetCore.Mvc;
using MES.Auth.Services;
using MES.Core.Models;
using MES.Core.DTOs.Auth;

namespace MES.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ApiResponse<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        return await _authService.LoginAsync(request);
    }

    [HttpPost("logout")]
    public async Task<ApiResponse<object>> Logout()
    {
        return await _authService.LogoutAsync();
    }

    [HttpPost("refresh-token")]
    public async Task<ApiResponse<LoginResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        return await _authService.RefreshTokenAsync(request.RefreshToken);
    }

    [HttpGet("current-user")]
    public async Task<ApiResponse<UserInfoResponse>> GetCurrentUser()
    {
        return await _authService.GetCurrentUserAsync();
    }
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
