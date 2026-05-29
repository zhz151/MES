using System.Text.Json;
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 原锁计划及执行前端服务
/// </summary>
public class RawMaterialLockPlanAndExecutionService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/raw-material-lock-plan";

    public RawMaterialLockPlanAndExecutionService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<RawMaterialLockPlanAndExecutionDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";

            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<RawMaterialLockPlanAndExecutionDto>>>(url);
            return response ?? ApiResponse<PagedResult<RawMaterialLockPlanAndExecutionDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<RawMaterialLockPlanAndExecutionDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> PlanArrangementAsync()
    {
        try
        {
            var response = await _http.PostAsJsonAsync<object?, ApiResponse<int>>($"{BaseUrl}/plan-arrangement", null);
            return response ?? ApiResponse<int>.Fail("计划安排失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> ExecuteDataUpdateAsync()
    {
        try
        {
            var response = await _http.PostAsJsonAsync<object?, ApiResponse<int>>($"{BaseUrl}/execute-data-update", null);
            return response ?? ApiResponse<int>.Fail("执行数据更新失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"网络错误: {ex.Message}");
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
}
