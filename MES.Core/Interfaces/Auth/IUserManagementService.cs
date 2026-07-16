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
}
