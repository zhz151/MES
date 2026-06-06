using System.Text.Json;
using MES.Core.DTOs;
using MES.Shared.Constants;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 日产估算前端服务
/// </summary>
public class DailyOutputEstimateService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.DailyOutputEstimate;

    public DailyOutputEstimateService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<DailyOutputEstimateDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<DailyOutputEstimateDto>>>(url);
            return response ?? ApiResponse<PagedResult<DailyOutputEstimateDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<DailyOutputEstimateDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<DailyOutputEstimateDto>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<DailyOutputEstimateDto>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<DailyOutputEstimateDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<DailyOutputEstimateDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveAsync(DailyOutputEstimateDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<DailyOutputEstimateDto, ApiResponse<bool>>($"{BaseUrl}/save", dto);
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
            var response = await _http.DeleteFromJsonAsync<ApiResponse<bool>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<bool>.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<DailyOutputEstimateDto>>> GetAllAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<DailyOutputEstimateDto>>>($"{BaseUrl}/all");
            return response ?? ApiResponse<List<DailyOutputEstimateDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<DailyOutputEstimateDto>>.Fail($"网络错误: {ex.Message}");
        }
    }
}
