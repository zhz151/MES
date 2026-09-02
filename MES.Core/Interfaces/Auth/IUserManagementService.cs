using MES.Core.DTOs.Auth;
using MES.Core.Models;

namespace MES.Core.Interfaces.Auth;

public interface IUserManagementService
{
    Task<ApiResponse<PagedResult<UserDto>>> GetPagedAsync(int pageIndex, int pageSize,
        string? keyword = null, string? sortBy = null, bool isDescending = true);
    Task<ApiResponse<UserDto>> CreateAsync(CreateUserRequest request);
    Task<ApiResponse<UserDto>> UpdateAsync(string userId, UpdateUserRequest request);
    Task<ApiResponse<object>> ResetPasswordAsync(string userId, ResetPasswordRequest request);
    Task<ApiResponse<object>> DeleteAsync(string userId);

    /// <summary>
    /// 按用户名反查用户 Id（用于「员工删除 → 登录账号联动删除」），不存在返回 null
    /// </summary>
    Task<string?> FindIdByUserNameAsync(string userName);
}
