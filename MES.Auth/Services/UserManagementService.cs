using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Auth;
using MES.Core.Interfaces.Auth;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Auth;

namespace MES.Auth.Services;

public class UserManagementService : IUserManagementService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _context;

    public UserManagementService(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    public async Task<ApiResponse<PagedResult<UserDto>>> GetPagedAsync(
        int pageIndex, int pageSize,
        string? keyword = null, string? sortBy = null, bool isDescending = true)
    {
        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(u => u.UserName!.Contains(kw) || u.Email!.Contains(kw) || (u.FullName ?? "").Contains(kw));
        }

        var totalCount = await query.CountAsync();

        // 排序（IdentityUser 无 CreatedTime，默认按邮箱排序）
        sortBy = sortBy?.ToLower() ?? "email";
        query = sortBy switch
        {
            "username" => isDescending ? query.OrderByDescending(u => u.UserName) : query.OrderBy(u => u.UserName),
            "email" => isDescending ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            "fullname" => isDescending ? query.OrderByDescending(u => u.FullName) : query.OrderBy(u => u.FullName),
            "isactive" => isDescending ? query.OrderByDescending(u => u.IsActive) : query.OrderBy(u => u.IsActive),
            "lastloginat" => isDescending ? query.OrderByDescending(u => u.LastLoginAt) : query.OrderBy(u => u.LastLoginAt),
            _ => isDescending ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
        };

        var users = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            dtos.Add(new UserDto
            {
                Id = user.Id,
                UserName = user.UserName ?? "",
                Email = user.Email ?? "",
                FullName = user.FullName,
                IsActive = user.IsActive,
                Roles = roles.ToList(),
                LastLoginAt = user.LastLoginAt
            });
        }

        return ApiResponse<PagedResult<UserDto>>.Ok(new PagedResult<UserDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        });
    }

    public async Task<ApiResponse<UserDto>> CreateAsync(CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return ApiResponse<UserDto>.Fail("邮箱不能为空");
        if (string.IsNullOrWhiteSpace(request.Password))
            return ApiResponse<UserDto>.Fail("密码不能为空");

        var existingUser = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (existingUser != null)
            return ApiResponse<UserDto>.Fail("该邮箱已被注册");

        var userName = string.IsNullOrWhiteSpace(request.UserName) ? request.Email.Trim() : request.UserName.Trim();
        var user = new AppUser
        {
            UserName = userName,
            Email = request.Email.Trim(),
            FullName = request.FullName?.Trim(),
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return ApiResponse<UserDto>.Fail(string.Join("; ", result.Errors.Select(e => e.Description)));

        // 分配角色
        if (request.Roles.Count > 0)
        {
            var roleResult = await _userManager.AddToRolesAsync(user, request.Roles);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                return ApiResponse<UserDto>.Fail(string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            }
        }

        var dto = new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            Email = user.Email ?? "",
            FullName = user.FullName,
            IsActive = user.IsActive,
            Roles = request.Roles.ToList(),
        };
        return ApiResponse<UserDto>.Ok(dto, "创建成功");
    }

    public async Task<ApiResponse<UserDto>> UpdateAsync(string userId, UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return ApiResponse<UserDto>.Fail("用户不存在");

        user.FullName = request.FullName?.Trim();
        user.IsActive = request.IsActive;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return ApiResponse<UserDto>.Fail(string.Join("; ", updateResult.Errors.Select(e => e.Description)));

        // 更新角色：移除现有角色，分配新角色
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Any())
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
                return ApiResponse<UserDto>.Fail("角色更新失败");
        }

        if (request.Roles.Count > 0)
        {
            var addResult = await _userManager.AddToRolesAsync(user, request.Roles);
            if (!addResult.Succeeded)
                return ApiResponse<UserDto>.Fail(string.Join("; ", addResult.Errors.Select(e => e.Description)));
        }

        var dto = new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            Email = user.Email ?? "",
            FullName = user.FullName,
            IsActive = user.IsActive,
            Roles = request.Roles.ToList(),
            LastLoginAt = user.LastLoginAt
        };
        return ApiResponse<UserDto>.Ok(dto, "更新成功");
    }

    public async Task<ApiResponse<object>> ResetPasswordAsync(string userId, ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return ApiResponse<object>.Fail("新密码不能为空");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return ApiResponse<object>.Fail("用户不存在");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
            return ApiResponse<object>.Fail(string.Join("; ", result.Errors.Select(e => e.Description)));

        return ApiResponse<object>.Ok(new object(), "密码重置成功");
    }

    public async Task<ApiResponse<object>> DeleteAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return ApiResponse<object>.Fail("用户不存在");

        // 不允许删除 admin 账号
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Any(r => r == "Admin"))
            return ApiResponse<object>.Fail("不能删除管理员账号");

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return ApiResponse<object>.Fail(string.Join("; ", result.Errors.Select(e => e.Description)));

        return ApiResponse<object>.Ok(new object(), "删除成功");
    }
}
