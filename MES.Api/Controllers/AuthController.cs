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
    
    /// <summary>
    /// 用户注册
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<LoginResponse>.Fail("请求参数无效"));
        var result = await _authService.RegisterAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
    
    /// <summary>
    /// 用户登录
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<LoginResponse>.Fail("请求参数无效"));
        var result = await _authService.LoginAsync(request);
        if (!result.Success) return Unauthorized(result);
        return Ok(result);
    }
    
    /// <summary>
    /// 刷新访问令牌
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Refresh([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken);
        if (!result.Success) return Unauthorized(result);
        return Ok(result);
    }
    
    /// <summary>
    /// 用户登出
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> Logout()
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userId)) return BadRequest(ApiResponse<bool>.Fail("用户不存在"));
        var result = await _authService.LogoutAsync(userId);
        return Ok(result);
    }
    
    /// <summary>
    /// 获取当前用户信息
    /// </summary>
    [HttpGet("current-user")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserInfoResponse>>> GetCurrentUser()
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        
        var result = await _authService.GetUserInfoAsync(userId);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }
    
    /// <summary>
    /// 修改用户密码
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        
        var result = await _authService.ChangePasswordAsync(userId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
    
    /// <summary>
    /// 获取用户权限列表
    /// </summary>
    [HttpGet("permissions")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetUserPermissions()
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        
        var result = await _authService.GetUserPermissionsAsync(userId);
        return Ok(result);
    }
    
    /// <summary>
    /// 健康检查端点（用于测试API连通性）
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public ActionResult<string> Health()
    {
        return Ok("MES API - Authentication Service is running");
    }
}