using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MES.Data;
using MES.Common.DTOs;

namespace MES.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class DbAdminController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<DbAdminController> _logger;

    public DbAdminController(
        AppDbContext dbContext,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<DbAdminController> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    [HttpGet("health")]
    public async Task<ActionResult<ApiResponse<object>>> HealthCheck()
    {
        try
        {
            // 检查数据库连接
            var canConnect = await _dbContext.Database.CanConnectAsync();

            var userCount = await _userManager.Users.CountAsync();
            var roleCount = await _roleManager.Roles.CountAsync();

            var result = new
            {
                DatabaseConnected = canConnect,
                UserCount = userCount,
                RoleCount = roleCount
            };

            return Ok(ApiResponse<object>.Ok(result, "数据库状态正常"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据库健康检查失败");
            return BadRequest(ApiResponse<object>.Fail($"数据库检查失败: {ex.Message}"));
        }
    }

    [HttpPost("create-admin")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> CreateAdminUser()
    {
        try
        {
            // 创建管理员角色（如果不存在）
            if (!await _roleManager.RoleExistsAsync("Admin"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Admin"));
                _logger.LogInformation("创建Admin角色");
            }

            if (!await _roleManager.RoleExistsAsync("User"))
            {
                await _roleManager.CreateAsync(new IdentityRole("User"));
                _logger.LogInformation("创建User角色");
            }

            // 检查管理员用户是否已存在
            var adminEmail = "admin@mes.com";
            var existingUser = await _userManager.FindByEmailAsync(adminEmail);

            if (existingUser != null)
            {
                return Ok(ApiResponse<LoginResponse>.Fail("管理员用户已存在"));
            }

            // 创建管理员用户
            var adminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            // 密码: Admin@123456 (符合密码策略)
            var result = await _userManager.CreateAsync(adminUser, "Admin@123456");

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(ApiResponse<LoginResponse>.Fail($"创建管理员失败: {errors}"));
            }

            // 分配管理员角色
            await _userManager.AddToRoleAsync(adminUser, "Admin");
            await _userManager.AddToRoleAsync(adminUser, "User");

            _logger.LogInformation("管理员用户创建成功: {Email}", adminEmail);

            return Ok(ApiResponse<LoginResponse>.Ok(null!, "管理员用户创建成功\n邮箱: admin@mes.com\n密码: Admin@123456"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建管理员用户失败");
            return BadRequest(ApiResponse<LoginResponse>.Fail($"创建失败: {ex.Message}"));
        }
    }

    [HttpPost("initialize")]
    public async Task<ActionResult<ApiResponse<string>>> InitializeDatabase()
    {
        try
        {
            // 创建/更新数据库
            await _dbContext.Database.EnsureCreatedAsync();
            _logger.LogInformation("数据库初始化成功");

            // 创建默认角色
            if (!await _roleManager.RoleExistsAsync("Admin"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Admin"));
                _logger.LogInformation("创建Admin角色");
            }

            if (!await _roleManager.RoleExistsAsync("User"))
            {
                await _roleManager.CreateAsync(new IdentityRole("User"));
                _logger.LogInformation("创建User角色");
            }

            // 创建管理员用户
            var adminEmail = "admin@mes.com";
            var existingUser = await _userManager.FindByEmailAsync(adminEmail);

            if (existingUser == null)
            {
                var adminUser = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(adminUser, "Admin@123456");

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(adminUser, "Admin");
                    await _userManager.AddToRoleAsync(adminUser, "User");
                    _logger.LogInformation("管理员用户创建成功");
                }
            }

            return Ok(ApiResponse<string>.Ok("数据库初始化完成\n\n默认管理员账号:\n邮箱: admin@mes.com\n密码: Admin@123456"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据库初始化失败");
            return BadRequest(ApiResponse<string>.Fail($"初始化失败: {ex.Message}"));
        }
    }
}
