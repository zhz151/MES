using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Scheduling;

namespace MES.Blazor.Services;

/// <summary>
/// 冷轧产能配置 Blazor 前端服务（查询 + 手工调整保存，反向同步由后端完成）
/// </summary>
public class ColdRollCapacityService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.ColdRollCapacity;

    public ColdRollCapacityService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<ColdRollCapacityDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<ColdRollCapacityDto>>>(url);
            return response ?? ApiResponse<PagedResult<ColdRollCapacityDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<ColdRollCapacityDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<ColdRollCapacityDto>>> GetAllAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<ColdRollCapacityDto>>>($"{BaseUrl}/all");
            return response ?? ApiResponse<List<ColdRollCapacityDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<ColdRollCapacityDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveAsync(ColdRollCapacityDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<ColdRollCapacityDto, ApiResponse<bool>>($"{BaseUrl}/save", dto);
            return response ?? ApiResponse<bool>.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
        }
    }
}
