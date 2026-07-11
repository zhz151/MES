using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Services;

public class ConfigParameterService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.ConfigParameter;

    public ConfigParameterService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<ConfigParameterDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<ConfigParameterDto>>>(url);
            return response ?? ApiResponse<PagedResult<ConfigParameterDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<ConfigParameterDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<ConfigParameterDto>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<ConfigParameterDto>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<ConfigParameterDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<ConfigParameterDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveAsync(ConfigParameterDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<ConfigParameterDto, ApiResponse<bool>>($"{BaseUrl}/save", dto);
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
