using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.WorkOrder;

namespace MES.Blazor.Services;

/// <summary>
/// 工单排程前端服务
/// </summary>
public class WorkOrderScheduleService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.WorkOrderSchedule;

    public WorkOrderScheduleService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<WorkOrderScheduleDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";

            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<WorkOrderScheduleDto>>>(url);
            return response ?? ApiResponse<PagedResult<WorkOrderScheduleDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<WorkOrderScheduleDto>>.Fail($"网络错误: {ex.Message}");
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

    public async Task<ApiResponse<bool>> SavePlanAsync(SaveWorkOrderPlanRequest request)
    {
        try
        {
            var result = await _http.PostAsJsonAsync<SaveWorkOrderPlanRequest, ApiResponse<bool>>($"{BaseUrl}/save-plan", request);
            return result ?? ApiResponse<bool>.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"保存失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> PlanScheduleAllAsync(QueryParams query)
    {
        try
        {
            var result = await _http.PostAsJsonAsync<QueryParams, ApiResponse<bool>>($"{BaseUrl}/plan-all", query);
            return result ?? ApiResponse<bool>.Fail("计划安排失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"计划安排失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> PlanScheduleKeepAttentionAsync(QueryParams query)
    {
        try
        {
            var result = await _http.PostAsJsonAsync<QueryParams, ApiResponse<bool>>($"{BaseUrl}/plan-keep-attention", query);
            return result ?? ApiResponse<bool>.Fail("进度保留计划失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"进度保留计划失败: {ex.Message}");
        }
    }
}
