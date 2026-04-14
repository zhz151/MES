using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using MES.Common.DTOs;
using MES.Services;

namespace MES.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    
    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }
    
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<LoginResponse>.Fail("请求参数无效"));
        var result = await _authService.RegisterAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
    
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<LoginResponse>.Fail("请求参数无效"));
        var result = await _authService.LoginAsync(request);
        if (!result.Success) return Unauthorized(result);
        return Ok(result);
    }
    
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Refresh([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken);
        if (!result.Success) return Unauthorized(result);
        return Ok(result);
    }
    
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> Logout()
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userId)) return BadRequest(ApiResponse<bool>.Fail("用户不存在"));
        var result = await _authService.LogoutAsync(userId);
        return Ok(result);
    }
    
    [HttpGet("test")]
    [Authorize]
    public ActionResult<ApiResponse<string>> Test()
    {
        return Ok(ApiResponse<string>.Ok($"认证成功，当前用户: {User.Identity?.Name}"));
    }
}
