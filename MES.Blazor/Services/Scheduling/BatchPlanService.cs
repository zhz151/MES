using System.Text.Json;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Scheduling;

namespace MES.Blazor.Services;

/// <summary>
/// 在产明细计划前端服务
/// </summary>
public class BatchPlanService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.BatchPlan;

    public BatchPlanService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<BatchPlanDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";

            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<BatchPlanDto>>>(url);
            return response ?? ApiResponse<PagedResult<BatchPlanDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<BatchPlanDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts");
            return response ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<List<BatchPlanSummaryRowDto>> GetSummaryAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<BatchPlanSummaryRowDto>>>($"{BaseUrl}/summary");
            return response?.Data ?? new List<BatchPlanSummaryRowDto>();
        }
        catch
        {
            return new List<BatchPlanSummaryRowDto>();
        }
    }

    public async Task<List<BatchPlanMonthlySummaryRowDto>> GetMonthlySummaryAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<BatchPlanMonthlySummaryRowDto>>>($"{BaseUrl}/monthly-summary");
            return response?.Data ?? new List<BatchPlanMonthlySummaryRowDto>();
        }
        catch
        {
            return new List<BatchPlanMonthlySummaryRowDto>();
        }
    }

    public async Task<BatchPlanOutsourcePendingDto?> GetOutsourcePendingAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<BatchPlanOutsourcePendingDto>>($"{BaseUrl}/outsource-pending");
            return response?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<BatchPlanDto>> GetAllAsync(string? sectionTab)
    {
        try
        {
            var url = $"{BaseUrl}/all";
            if (!string.IsNullOrEmpty(sectionTab))
                url += $"?sectionTab={Uri.EscapeDataString(sectionTab)}";
            var response = await _http.GetFromJsonAsync<ApiResponse<List<BatchPlanDto>>>(url);
            return response?.Data ?? new List<BatchPlanDto>();
        }
        catch
        {
            return new List<BatchPlanDto>();
        }
    }

    public async Task<List<BatchPlanSectionTabDto>> GetSectionTabOptionsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<BatchPlanSectionTabDto>>>($"{BaseUrl}/section-tab-options");
            return response?.Data ?? new List<BatchPlanSectionTabDto>();
        }
        catch
        {
            return new List<BatchPlanSectionTabDto>();
        }
    }

}
