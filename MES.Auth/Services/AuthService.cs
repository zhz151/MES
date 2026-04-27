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
using MES.Core.DTOs.Auth;
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
        // Parameter validation
        if (string.IsNullOrEmpty(request.Email))
        {
            return ApiResponse<LoginResponse>.Fail("Email cannot be empty");
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            return ApiResponse<LoginResponse>.Fail("Password cannot be empty");
        }

        // Find user
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return ApiResponse<LoginResponse>.Fail("Invalid username or password");
        }

        // Verify password
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        if (!result.Succeeded)
        {
            return ApiResponse<LoginResponse>.Fail("Invalid username or password");
        }

        // Check user status
        if (!user.IsActive)
        {
            return ApiResponse<LoginResponse>.Fail("User account has been disabled, please contact administrator");
        }

        // Get user roles
        var roles = await _userManager.GetRolesAsync(user);

        // Generate JWT token
        var token = await _jwtService.GenerateTokenAsync(user, roles);

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
            RefreshTokenExpires = refreshToken.Expires,
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
    /// Get current user information
    /// </summary>
    public async Task<ApiResponse<UserInfoResponse>> GetCurrentUserAsync()
    {
        await Task.CompletedTask;
        return ApiResponse<UserInfoResponse>.Fail("Get current user information feature to be implemented");
    }

    /// <summary>
    /// Logout
    /// </summary>
    public async Task<ApiResponse<object>> LogoutAsync()
    {
        await _signInManager.SignOutAsync();
        return ApiResponse<object>.Ok(new object(), "Logout successful");
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
        var token = await _jwtService.GenerateTokenAsync(user, roles);

        // Get JWT settings
        var jwtSettings = _configuration.GetSection("JwtSettings").Get<JwtSettings>();

        // Generate new refresh token
        var newRefreshToken = await GenerateAndStoreRefreshTokenAsync(user.Id);

        var loginResponse = new LoginResponse
        {
            Token = token,
            RefreshToken = newRefreshToken.Token,
            RefreshTokenExpires = newRefreshToken.Expires,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            Roles = roles.ToList(),
            Expires = DateTime.UtcNow.AddMinutes(jwtSettings?.ExpireMinutes ?? 480),
            FullName = user.FullName ?? string.Empty
        };

        return ApiResponse<LoginResponse>.Ok(loginResponse);
    }
}

/// <summary>
/// JWT service implementation
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
            throw new InvalidOperationException("JWT configuration not found");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var userId = user.Id ?? throw new InvalidOperationException("User ID cannot be empty");
        var userEmail = user.Email ?? throw new InvalidOperationException("User email cannot be empty");
        var userName = user.UserName ?? throw new InvalidOperationException("Username cannot be empty");
        var fullName = user.FullName ?? string.Empty;

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, userEmail),
            new Claim(JwtRegisteredClaimNames.Name, userName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("fullName", fullName)
        };

        // Add role claims
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