using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Services;

public class StandardWorkDayDeliveryStateService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.StandardWorkDayDeliveryState;

    public StandardWorkDayDeliveryStateService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<StandardWorkDayDeliveryStateDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<StandardWorkDayDeliveryStateDto>>>(url);
            return response ?? ApiResponse<PagedResult<StandardWorkDayDeliveryStateDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<StandardWorkDayDeliveryStateDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<StandardWorkDayDeliveryStateDto?>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<StandardWorkDayDeliveryStateDto?>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<StandardWorkDayDeliveryStateDto?>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<StandardWorkDayDeliveryStateDto?>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveAsync(StandardWorkDayDeliveryStateDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<StandardWorkDayDeliveryStateDto, ApiResponse<bool>>($"{BaseUrl}/save", dto);
            return response ?? ApiResponse<bool>.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<object?, ApiResponse<bool>>($"{BaseUrl}/delete/{id}", null);
            return response ?? ApiResponse<bool>.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
        }
    }
}
