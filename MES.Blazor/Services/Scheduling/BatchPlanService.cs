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

    public async Task<List<ColdRollScheduleSummaryDto>> GetFlowSummaryAsync(string? sectionTab, int? maxDiff = null)
    {
        try
        {
            var url = $"{BaseUrl}/flow-summary";
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(sectionTab))
                queryParams.Add($"sectionTab={Uri.EscapeDataString(sectionTab)}");
            if (maxDiff.HasValue)
                queryParams.Add($"maxDiff={maxDiff.Value}");
            if (queryParams.Count > 0)
                url += "?" + string.Join("&", queryParams);
            var response = await _http.GetFromJsonAsync<ApiResponse<List<ColdRollScheduleSummaryDto>>>(url);
            return response?.Data ?? new List<ColdRollScheduleSummaryDto>();
        }
        catch
        {
            return new List<ColdRollScheduleSummaryDto>();
        }
    }
}
