// �ļ�·��: MES.Api/Controllers/AuthController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Interfaces.Auth;
using MES.Core.Models;
using MES.Core.DTOs.Auth;

namespace MES.Api.Controllers.Infrastructure;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest loginRequest)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<LoginResponse>.Fail("请求参数无效"));
        var result = await _authService.LoginAsync(loginRequest);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object>>> Logout()
    {
        var result = await _authService.LogoutAsync();
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh-token")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> RefreshToken([FromBody] RefreshTokenRequest refreshTokenRequest)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<LoginResponse>.Fail("请求参数无效"));
        var result = await _authService.RefreshTokenAsync(refreshTokenRequest.RefreshToken);
        return Ok(result);
    }

    [HttpGet("current-user")]
    public async Task<ActionResult<ApiResponse<UserInfoResponse>>> GetCurrentUser()
    {
        var result = await _authService.GetCurrentUserAsync();
        return Ok(result);
    }
}
