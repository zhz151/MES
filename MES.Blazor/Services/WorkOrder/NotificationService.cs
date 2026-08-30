using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.WorkOrder;

namespace MES.Blazor.Services;

/// <summary>
/// 通知前端服务
/// </summary>
public class NotificationService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.Notification;

    public NotificationService(AuthHttpClient http)
    {
        _http = http;
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