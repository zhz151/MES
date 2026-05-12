using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 通知前端服务
/// </summary>
public class NotificationService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/notification";

    public NotificationService(AuthHttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// 获取未读通知数量
    /// </summary>
    public async Task<ApiResponse<int>> GetUnreadCountAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<int>>($"{BaseUrl}/unread-count");
            return response ?? ApiResponse<int>.Fail("获取未读数量失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 分页获取通知列表
    /// </summary>
    public async Task<ApiResponse<PagedResult<NotificationDto>>> GetPagedAsync(int pageIndex, int pageSize)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={pageIndex}&pageSize={pageSize}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<NotificationDto>>>(url);
            return response ?? ApiResponse<PagedResult<NotificationDto>>.Fail("获取通知列表失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<NotificationDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 标记单条通知为已读
    /// </summary>
    public async Task<ApiResponse<object>> MarkAsReadAsync(int id)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<object, ApiResponse<object>>($"{BaseUrl}/{id}/read", new { });
            return response ?? ApiResponse<object>.Fail("标记失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 标记所有通知为已读
    /// </summary>
    public async Task<ApiResponse<object>> MarkAllAsReadAsync()
    {
        try
        {
            var response = await _http.PostAsJsonAsync<object, ApiResponse<object>>($"{BaseUrl}/read-all", new { });
            return response ?? ApiResponse<object>.Fail("标记失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取指定类型的未读通知列表
    /// </summary>
    public async Task<ApiResponse<List<NotificationDto>>> GetByTypeAsync(string type)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<NotificationDto>>>($"{BaseUrl}/by-type/{type}");
            return response ?? ApiResponse<List<NotificationDto>>.Fail("获取通知失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<NotificationDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 标记指定类型的所有通知为已读
    /// </summary>
    public async Task<ApiResponse<object>> MarkAllByTypeAsReadAsync(string type)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<object, ApiResponse<object>>($"{BaseUrl}/mark-type/{type}/read", new { });
            return response ?? ApiResponse<object>.Fail("标记失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail($"网络错误: {ex.Message}");
        }
    }
}