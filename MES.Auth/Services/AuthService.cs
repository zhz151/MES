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

    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request)
    {
        try
        {
            // 查找用户
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return await Task.FromResult(ApiResponse<LoginResponse>.Fail("用户名或密码错误"));
            }

            // 验证密码
            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (!result.Succeeded)
            {
                return await Task.FromResult(ApiResponse<LoginResponse>.Fail("用户名或密码错误"));
            }

            // 检查用户状态
            if (!user.IsActive)
            {
                return await Task.FromResult(ApiResponse<LoginResponse>.Fail("用户账户已被禁用，请联系管理员"));
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
            
            // 返回登录响应
            var loginResponse = new LoginResponse
            {
                Token = token,
                Email = user.Email!,
                UserName = user.UserName!,
                Roles = roles.ToList(),
                Expires = DateTime.UtcNow.AddMinutes(jwtSettings?.ExpireMinutes ?? 480),
                FullName = user.FullName
            };

            return await Task.FromResult(ApiResponse<LoginResponse>.Ok(loginResponse));
        }
        catch (Exception ex)
        {
            return await Task.FromResult(ApiResponse<LoginResponse>.Fail("登录失败：" + ex.Message));
        }
    }

    public async Task<ApiResponse<UserInfoResponse>> GetCurrentUserAsync()
    {
        try
        {
            // 从HttpContext获取当前用户（需要注入IHttpContextAccessor）
            // 这里简化实现，实际使用时需要从Claims中获取用户信息
            return await Task.FromResult(ApiResponse<UserInfoResponse>.Fail("获取当前用户信息功能待实现"));
        }
        catch (Exception ex)
        {
            return await Task.FromResult(ApiResponse<UserInfoResponse>.Fail("获取用户信息失败：" + ex.Message));
        }
    }

    public async Task<ApiResponse<object>> LogoutAsync()
    {
        try
        {
            await _signInManager.SignOutAsync();
            return await Task.FromResult(ApiResponse<object>.Ok(null, "注销成功"));
        }
        catch (Exception ex)
        {
            return await Task.FromResult(ApiResponse<object>.Fail("注销失败：" + ex.Message));
        }
    }

    public async Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string refreshToken)
    {
        // TODO: 实现刷新令牌逻辑
        return await Task.FromResult(ApiResponse<LoginResponse>.Fail("刷新令牌功能待实现"));
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

    public async Task<string> GenerateTokenAsync(AppUser user, IList<string> roles)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings").Get<JwtSettings>();
        if (jwtSettings == null)
        {
            throw new InvalidOperationException("JWT配置未找到");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Name, user.UserName!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("fullName", user.FullName)
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

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GetUsernameFromToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
        return jwtToken?.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name)?.Value ?? string.Empty;
    }

    public bool ValidateToken(string token)
    {
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