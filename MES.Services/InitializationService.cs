using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace MES.Services;

public interface IInitializationService
{
    Task InitializeAsync();
}

public class InitializationService : IInitializationService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<InitializationService> _logger;

    public InitializationService(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<InitializationService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await CreateDefaultRolesAsync();
        await CreateAdminUserAsync();
    }

    private async Task CreateDefaultRolesAsync()
    {
        var roles = new[] { "Admin", "Manager", "User" };
        
        foreach (var roleName in roles)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
                _logger.LogInformation("已创建角色: {RoleName}", roleName);
            }
        }
    }

    private async Task CreateAdminUserAsync()
    {
        const string adminEmail = "admin@mes.com";
        const string adminUsername = adminEmail;
        const string adminPassword = "Admin@123456";
        
        var existingAdmin = await _userManager.FindByEmailAsync(adminEmail);
        
        if (existingAdmin == null)
        {
            var adminUser = new IdentityUser
            {
                UserName = adminUsername,
                Email = adminEmail,
                EmailConfirmed = true,
                PhoneNumber = null,
                PhoneNumberConfirmed = false
            };

            var result = await _userManager.CreateAsync(adminUser, adminPassword);
            
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(adminUser, "Admin");
                _logger.LogInformation("已创建管理员账号: {Email}", adminEmail);
            }
            else
            {
                _logger.LogError("创建管理员账号失败: {Errors}", 
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            // 重置管理员密码以确保准确性
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(existingAdmin);
            var result = await _userManager.ResetPasswordAsync(existingAdmin, resetToken, adminPassword);
            
            if (result.Succeeded)
            {
                // 确保管理员具有Admin角色
                var roles = await _userManager.GetRolesAsync(existingAdmin);
                if (!roles.Contains("Admin"))
                {
                    await _userManager.AddToRoleAsync(existingAdmin, "Admin");
                }
                
                _logger.LogInformation("已重置管理员密码: {Email}", adminEmail);
            }
            else
            {
                _logger.LogError("重置管理员密码失败: {Errors}", 
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}