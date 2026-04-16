// 文件路径: MES.Auth/Services/AuthService.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MES.Data.Entities;
using MES.Core.DTOs.Auth;
using MES.Core.Models;
using MES.Data;
using MES.Shared.Settings;

namespace MES.Auth.Services;

/// <summary>
/// 认证服务实现
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly IJwtService _jwtService;
    private readonly AppDbContext _context;

    public AuthService(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IConfiguration configuration,
        IJwtService jwtService,
        AppDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _jwtService = jwtService;
        _context = context;
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request)
    {
        // 参数验证
        if (string.IsNullOrEmpty(request.Email))
        {
            return ApiResponse<LoginResponse>.Fail("邮箱不能为空");
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            return ApiResponse<LoginResponse>.Fail("密码不能为空");
        }

        // 查找用户
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return ApiResponse<LoginResponse>.Fail("用户名或密码错误");
        }

        // 验证密码
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        if (!result.Succeeded)
        {
            return ApiResponse<LoginResponse>.Fail("用户名或密码错误");
        }

        // 检查用户状态
        if (!user.IsActive)
        {
            return ApiResponse<LoginResponse>.Fail("用户账户已被禁用，请联系管理员");
        }

        // 获取用户角色
        var roles = await _userManager.GetRolesAsync(user);

        // 生成JWT令牌
        var token = await _jwtService.GenerateTokenAsync(user, roles);

        // 更新最后登录时间
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // 获取JWT配置
        var jwtSettings = _configuration.GetSection("JwtSettings").Get<JwtSettings>();

        // 返回登录响应 - 使用 null 合并运算符处理可能的 null 值
        var loginResponse = new LoginResponse
        {
            Token = token,
            Email = user.Email ?? string.Empty,      // 修复 CS8601
            UserName = user.UserName ?? string.Empty, // 修复 CS8601
            Roles = roles.ToList(),
            Expires = DateTime.UtcNow.AddMinutes(jwtSettings?.ExpireMinutes ?? 480),
            FullName = user.FullName ?? string.Empty  // 修复 CS8601
        };

        return ApiResponse<LoginResponse>.Ok(loginResponse);
    }

    /// <summary>
    /// 获取当前用户信息
    /// </summary>
    public async Task<ApiResponse<UserInfoResponse>> GetCurrentUserAsync()
    {
        // TODO: 从 HttpContext 获取当前用户
        // 这里返回一个占位响应，避免 CS1998 警告
        await Task.CompletedTask;  // 修复 CS1998 - 添加真正的异步等待

        return ApiResponse<UserInfoResponse>.Fail("获取当前用户信息功能待实现");
    }

    /// <summary>
    /// 注销登录
    /// </summary>
    public async Task<ApiResponse<object>> LogoutAsync()
    {
        await _signInManager.SignOutAsync();
        // 使用 ApiResponse<object>.Ok 并传入一个空对象或字符串
        return ApiResponse<object>.Ok(new object(), "注销成功");
    }

    /// <summary>
    /// 刷新令牌
    /// </summary>
    public async Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string refreshToken)
    {
        // 参数验证 - 修复 CS8625
        if (string.IsNullOrEmpty(refreshToken))
        {
            return ApiResponse<LoginResponse>.Fail("刷新令牌不能为空");
        }

        // TODO: 实现刷新令牌逻辑
        await Task.CompletedTask;  // 添加真正的异步等待

        return ApiResponse<LoginResponse>.Fail("刷新令牌功能待实现");
    }
}

/// <summary>
/// JWT服务实现
/// </summary>
public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<AppUser> _userManager;

    public JwtService(IConfiguration configuration, UserManager<AppUser> userManager)
    {
        _configuration = configuration;
        _userManager = userManager;
    }

    public Task<string> GenerateTokenAsync(AppUser user, IList<string> roles)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings").Get<JwtSettings>();
        if (jwtSettings == null)
        {
            throw new InvalidOperationException("JWT配置未找到");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // 修复 CS8604 - 使用 null 合并运算符确保非空
        var userId = user.Id ?? throw new InvalidOperationException("用户ID不能为空");
        var userEmail = user.Email ?? throw new InvalidOperationException("用户邮箱不能为空");
        var userName = user.UserName ?? throw new InvalidOperationException("用户名不能为空");
        var fullName = user.FullName ?? string.Empty;

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, userEmail),
            new Claim(JwtRegisteredClaimNames.Name, userName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("fullName", fullName)
        };

        // 添加角色声明
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: jwtSettings.Issuer,
            audience: jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(jwtSettings.ExpireMinutes),
            signingCredentials: creds
        );

        return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
    }

    public string GetUsernameFromToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return string.Empty;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
            return jwtToken?.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name)?.Value ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public bool ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        try
        {
            var jwtSettings = _configuration.GetSection("JwtSettings").Get<JwtSettings>();
            if (jwtSettings == null)
                return false;

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwtSettings.Secret);

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            return true;
        }
        catch
        {
            return false;
        }
    }
}