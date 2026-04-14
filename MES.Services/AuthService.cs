using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MES.Common.DTOs;
using Microsoft.Extensions.Logging;

namespace MES.Services;

public interface IAuthService
{
    Task<ApiResponse<LoginResponse>> RegisterAsync(RegisterRequest request);
    Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string refreshToken);
    Task<ApiResponse<bool>> LogoutAsync(string userId);
    Task<ApiResponse<UserInfoResponse>> GetUserInfoAsync(string userId);
    Task<ApiResponse<bool>> ChangePasswordAsync(string userId, ChangePasswordRequest request);
    Task<ApiResponse<List<string>>> GetUserPermissionsAsync(string userId);
}

public class AuthService : IAuthService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private static readonly Dictionary<string, string> _refreshTokens = new();
    
    public AuthService(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _logger = logger;
    }
    
    public async Task<ApiResponse<LoginResponse>> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
                return ApiResponse<LoginResponse>.Fail("该邮箱已注册");
            
            var user = new IdentityUser
            {
                UserName = request.Email,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                EmailConfirmed = true
            };
            
            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return ApiResponse<LoginResponse>.Fail(string.Join(", ", result.Errors.Select(e => e.Description)));
            
            // 默认赋予 User 角色
            await _userManager.AddToRoleAsync(user, "User");
            
            return await GenerateLoginResponse(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "注册失败: {Email}", request.Email);
            return ApiResponse<LoginResponse>.Fail("注册失败，请稍后重试");
        }
    }
    
    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(request.Username) 
                      ?? await _userManager.FindByNameAsync(request.Username);
            
            if (user == null)
                return ApiResponse<LoginResponse>.Fail("用户名或密码错误");
            
            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (!result.Succeeded)
                return ApiResponse<LoginResponse>.Fail("用户名或密码错误");
            
            return await GenerateLoginResponse(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录失败: {Username}", request.Username);
            return ApiResponse<LoginResponse>.Fail("登录失败，请稍后重试");
        }
    }
    
    public async Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            var userId = _refreshTokens.FirstOrDefault(x => x.Value == refreshToken).Key;
            if (string.IsNullOrEmpty(userId))
                return ApiResponse<LoginResponse>.Fail("无效的刷新令牌");
            
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ApiResponse<LoginResponse>.Fail("用户不存在");
            
            return await GenerateLoginResponse(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新令牌失败");
            return ApiResponse<LoginResponse>.Fail("刷新令牌失败");
        }
    }
    
    public async Task<ApiResponse<bool>> LogoutAsync(string userId)
    {
        try
        {
            if (_refreshTokens.ContainsKey(userId))
                _refreshTokens.Remove(userId);
            
            return ApiResponse<bool>.Ok(true, "登出成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登出失败: {UserId}", userId);
            return ApiResponse<bool>.Fail("登出失败");
        }
    }
    
    public async Task<ApiResponse<UserInfoResponse>> GetUserInfoAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ApiResponse<UserInfoResponse>.Fail("用户不存在");
            
            var roles = await _userManager.GetRolesAsync(user);
            var permissions = await GetUserPermissionsAsync(user);
            
            var userInfo = new UserInfoResponse
            {
                UserId = user.Id,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Roles = roles.ToList(),
                Permissions = permissions.Data?.ToList() ?? new List<string>()
            };
            
            return ApiResponse<UserInfoResponse>.Ok(userInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户信息失败: {UserId}", userId);
            return ApiResponse<UserInfoResponse>.Fail("获取用户信息失败");
        }
    }
    
    public async Task<ApiResponse<bool>> ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ApiResponse<bool>.Fail("用户不存在");
            
            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
                return ApiResponse<bool>.Fail(string.Join(", ", result.Errors.Select(e => e.Description)));
            
            return ApiResponse<bool>.Ok(true, "密码修改成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修改密码失败: {UserId}", userId);
            return ApiResponse<bool>.Fail("修改密码失败");
        }
    }
    
    public async Task<ApiResponse<List<string>>> GetUserPermissionsAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ApiResponse<List<string>>.Fail("用户不存在");
            
            return await GetUserPermissionsAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户权限失败: {UserId}", userId);
            return ApiResponse<List<string>>.Fail("获取用户权限失败");
        }
    }
    
    private async Task<ApiResponse<List<string>>> GetUserPermissionsAsync(IdentityUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var permissions = new List<string>();
        
        // 根据角色返回对应权限（简化版，实际应根据设计文档实现）
        foreach (var role in roles)
        {
            permissions.AddRange(GetPermissionsForRole(role));
        }
        
        return ApiResponse<List<string>>.Ok(permissions.Distinct().ToList());
    }
    
    private List<string> GetPermissionsForRole(string role)
    {
        return role.ToLower() switch
        {
            "admin" => new List<string> 
            { 
                "Permission:System.Dashboard", "Permission:System.UserManagement", "Permission:System.Settings",
                "Permission:Order.Create", "Permission:Order.View", "Permission:Order.Edit", "Permission:Order.Delete",
                "Permission:WorkOrder.Create", "Permission:WorkOrder.View", "Permission:WorkOrder.Edit", "Permission:WorkOrder.Delete",
                "Permission:Batch.Create", "Permission:Batch.View", "Permission:Batch.Edit", "Permission:Batch.Delete",
                "Permission:Quality.Create", "Permission:Quality.View", "Permission:Quality.Edit", "Permission:Quality.Delete",
                "Permission:Warehouse.Create", "Permission:Warehouse.View", "Permission:Warehouse.Edit", "Permission:Warehouse.Delete",
                "Permission:Equipment.Create", "Permission:Equipment.View", "Permission:Equipment.Edit", "Permission:Equipment.Delete",
                "Permission:Material.Create", "Permission:Material.View", "Permission:Material.Edit", "Permission:Material.Delete"
            },
            "manager" => new List<string> 
            { 
                "Permission:System.Dashboard", 
                "Permission:Order.View", "Permission:Order.Edit",
                "Permission:WorkOrder.View", "Permission:WorkOrder.Edit",
                "Permission:Batch.View", "Permission:Batch.Edit",
                "Permission:Quality.View", "Permission:Quality.Edit",
                "Permission:Warehouse.View", "Permission:Warehouse.Edit"
            },
            "user" => new List<string> 
            { 
                "Permission:System.Dashboard",
                "Permission:Order.View",
                "Permission:WorkOrder.View",
                "Permission:Batch.View",
                "Permission:Quality.View",
                "Permission:Warehouse.View"
            },
            _ => new List<string> { "Permission:System.Dashboard" }
        };
    }
    
    private async Task<ApiResponse<LoginResponse>> GenerateLoginResponse(IdentityUser user)
    {
        var token = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();
        
        _refreshTokens[user.Id] = refreshToken;
        
        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetUserPermissionsAsync(user);
        
        var response = new LoginResponse
        {
            Token = token,
            RefreshToken = refreshToken,
            Expiration = DateTime.UtcNow.AddHours(8), // 8小时有效期
            Username = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Roles = roles.ToList(),
            Permissions = permissions.Data?.ToList() ?? new List<string>()
        };
        
        return ApiResponse<LoginResponse>.Ok(response, "登录成功");
    }
    
    private string GenerateJwtToken(IdentityUser user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Name, user.UserName ?? string.Empty)
        };
        
        // 添加角色声明
        var roles = _userManager.GetRolesAsync(user).Result;
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        
        // 添加权限声明（用于扩展的权限系统）
        var permissions = GetUserPermissionsAsync(user).Result.Data ?? new List<string>();
        claims.AddRange(permissions.Select(permission => new Claim("Permission", permission)));
        
        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    
    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}