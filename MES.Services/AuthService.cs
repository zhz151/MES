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
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return ApiResponse<LoginResponse>.Fail($"注册失败: {errors}");
            }
            
            if (!await _roleManager.RoleExistsAsync("User"))
                await _roleManager.CreateAsync(new IdentityRole("User"));
            await _userManager.AddToRoleAsync(user, "User");
            
            _logger.LogInformation("用户注册成功: {Email}", request.Email);
            return await LoginAsync(new LoginRequest { Username = user.UserName, Password = request.Password });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "注册失败");
            return ApiResponse<LoginResponse>.Fail("注册失败，请稍后重试");
        }
    }
    
    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request)
    {
        try
        {
            var user = await _userManager.FindByNameAsync(request.Username);
            if (user == null) user = await _userManager.FindByEmailAsync(request.Username);
            if (user == null) return ApiResponse<LoginResponse>.Fail("用户名或密码错误");
            
            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (!result.Succeeded) return ApiResponse<LoginResponse>.Fail("用户名或密码错误");
            
            var roles = await _userManager.GetRolesAsync(user);
            var token = GenerateJwtToken(user, roles);
            var refreshToken = GenerateRefreshToken();
            
            _refreshTokens[user.Id] = refreshToken;
            
            return ApiResponse<LoginResponse>.Ok(new LoginResponse
            {
                Token = token,
                RefreshToken = refreshToken,
                Expiration = DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpireMinutes"])),
                Username = user.UserName ?? user.Email,
                Email = user.Email,
                Roles = roles.ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录失败");
            return ApiResponse<LoginResponse>.Fail("登录失败，请稍后重试");
        }
    }
    
    public async Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string refreshToken)
    {
        var userId = _refreshTokens.FirstOrDefault(x => x.Value == refreshToken).Key;
        if (string.IsNullOrEmpty(userId)) return ApiResponse<LoginResponse>.Fail("无效的RefreshToken");
        
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return ApiResponse<LoginResponse>.Fail("用户不存在");
        
        var roles = await _userManager.GetRolesAsync(user);
        var newToken = GenerateJwtToken(user, roles);
        var newRefreshToken = GenerateRefreshToken();
        
        _refreshTokens[user.Id] = newRefreshToken;
        
        return ApiResponse<LoginResponse>.Ok(new LoginResponse
        {
            Token = newToken,
            RefreshToken = newRefreshToken,
            Expiration = DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpireMinutes"])),
            Username = user.UserName ?? user.Email,
            Email = user.Email,
            Roles = roles.ToList()
        });
    }
    
    public Task<ApiResponse<bool>> LogoutAsync(string userId)
    {
        _refreshTokens.Remove(userId);
        return Task.FromResult(ApiResponse<bool>.Ok(true, "退出成功"));
    }
    
    private string GenerateJwtToken(IdentityUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.UserName ?? user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        foreach (var role in roles) claims.Add(new Claim(ClaimTypes.Role, role));
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpireMinutes"])),
            signingCredentials: creds));
    }
    
    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
