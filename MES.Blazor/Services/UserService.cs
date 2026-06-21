using System.Text.Json;
using MES.Core.DTOs.Auth;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services;

public class UserService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.Users;

    public UserService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<UserDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var isDesc = query.IsDescending ? "true" : "false";
            var encodedSortBy = Uri.EscapeDataString(query.SortBy ?? ApiEndpoints.DefaultSortBy);
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={encodedSortBy}&isDescending={isDesc}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<UserDto>>>(url)
                   ?? ApiResponse<PagedResult<UserDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<UserDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<UserDto>> CreateAsync(CreateUserRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateUserRequest, ApiResponse<UserDto>>(BaseUrl, request)
                   ?? ApiResponse<UserDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<UserDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<UserDto>> UpdateAsync(string userId, UpdateUserRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateUserRequest, ApiResponse<UserDto>>($"{BaseUrl}/{userId}", request)
                   ?? ApiResponse<UserDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<UserDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> ResetPasswordAsync(string userId, ResetPasswordRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<ResetPasswordRequest, ApiResponse<object>>($"{BaseUrl}/{userId}/reset-password", request)
                   ?? ApiResponse<object>.Fail("重置密码失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> DeleteAsync(string userId)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/{userId}")
                   ?? ApiResponse<object>.Fail("删除失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }
}
