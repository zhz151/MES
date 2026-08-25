// 文件路径: MES.Auth/Services/AuthService.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MES.Data.Entities;
using MES.Data.Entities.Auth;
using MES.Core.DTOs.Auth;
using MES.Core.Interfaces.Auth;
using MES.Core.Models;
using MES.Data;
using MES.Shared.Settings;

namespace MES.Auth.Services;

/// <summary>
/// Authentication service implementation
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
    /// User login
    /// </summary>
    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request)
    {
        // Parameter validation（标识字段 Email 语义 = 用户名或邮箱）
        if (string.IsNullOrEmpty(request.Email))
        {
            return ApiResponse<LoginResponse>.Fail("用户名/邮箱不能为空");
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            return ApiResponse<LoginResponse>.Fail("密码不能为空");
        }

        // Find user：先按用户名、再按邮箱匹配（用户名为主标识，存量邮箱用户兼容）
        var identifier = request.Email.Trim();
        var user = await _userManager.FindByNameAsync(identifier);
        user ??= await _userManager.FindByEmailAsync(identifier);
        if (user == null)
        {
            return ApiResponse<LoginResponse>.Fail("用户名或密码错误");
        }

        // Verify password
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        if (!result.Succeeded)
        {
            return ApiResponse<LoginResponse>.Fail("用户名或密码错误");
        }

        // Check user status
        if (!user.IsActive)
        {
            return ApiResponse<LoginResponse>.Fail("账号已被停用，请联系管理员");
        }

        // Get user roles
        var roles = await _userManager.GetRolesAsync(user);

        // Generate JWT token
        var token = await _jwtService.GenerateTokenAsync(new JwtGenerationRequest
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            FullName = user.FullName,
            Roles = roles
        });

        // Update last login time
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // Get JWT settings
        var jwtSettings = _configuration.GetSection("JwtSettings").Get<JwtSettings>();

        // Return login response
        var refreshToken = await GenerateAndStoreRefreshTokenAsync(user.Id);
        var loginResponse = new LoginResponse
        {
            Token = token,
            RefreshToken = refreshToken.Token,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            Roles = roles.ToList(),
            Expires = DateTime.UtcNow.AddMinutes(jwtSettings?.ExpireMinutes ?? 480),
            FullName = user.FullName ?? string.Empty
        };

        return ApiResponse<LoginResponse>.Ok(loginResponse);
    }

    /// <summary>
    /// Generate and store a refresh token
    /// </summary>
    private async Task<RefreshToken> GenerateAndStoreRefreshTokenAsync(string userId)
    {
        // Revoke old tokens for this user
        var oldTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();
        foreach (var old in oldTokens)
        {
            old.IsRevoked = true;
        }

        // Generate new refresh token
        var refreshToken = new RefreshToken
        {
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            UserId = userId,
            Expires = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        return refreshToken;
    }

    /// <summary>
    /// Refresh token
    /// </summary>
    public async Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string refreshToken)
    {
        // Parameter validation
        if (string.IsNullOrEmpty(refreshToken))
        {
            return ApiResponse<LoginResponse>.Fail("Refresh token cannot be empty");
        }

        // Find the stored refresh token
        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken && !rt.IsRevoked);

        if (storedToken == null)
        {
            return ApiResponse<LoginResponse>.Fail("Invalid refresh token");
        }

        if (storedToken.Expires < DateTime.UtcNow)
        {
            storedToken.IsRevoked = true;
            await _context.SaveChangesAsync();
            return ApiResponse<LoginResponse>.Fail("Refresh token has expired, please login again");
        }

        // Revoke the current refresh token (rotation)
        storedToken.IsRevoked = true;

        // Find user
        var user = await _userManager.FindByIdAsync(storedToken.UserId);
        if (user == null || !user.IsActive)
        {
            await _context.SaveChangesAsync();
            return ApiResponse<LoginResponse>.Fail("User account does not exist or has been disabled");
        }

        // Get user roles
        var roles = await _userManager.GetRolesAsync(user);

        // Generate new access token
        var token = await _jwtService.GenerateTokenAsync(new JwtGenerationRequest
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            FullName = user.FullName,
            Roles = roles
        });

        // Get JWT settings
        var jwtSettings = _configuration.GetSection("JwtSettings").Get<JwtSettings>();

        // Generate new refresh token
        var newRefreshToken = await GenerateAndStoreRefreshTokenAsync(user.Id);

        var loginResponse = new LoginResponse
        {
            Token = token,
            RefreshToken = newRefreshToken.Token,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            Roles = roles.ToList(),
            Expires = DateTime.UtcNow.AddMinutes(jwtSettings?.ExpireMinutes ?? 480),
            FullName = user.FullName ?? string.Empty
        };

        return ApiResponse<LoginResponse>.Ok(loginResponse);
    }
}
