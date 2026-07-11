using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.WorkOrder;

namespace MES.Blazor.Services;

/// <summary>
/// 工单执行状况前端服务
/// </summary>
public class WorkOrderExecutionService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.WorkOrderExecution;

    public WorkOrderExecutionService(AuthHttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// 分页查询工单执行状况列表
    /// </summary>
    public async Task<ApiResponse<PagedResult<WorkOrderExecutionSummaryDto>>> GetPagedAsync(QueryParams query, DateTime? signDateFrom = null, DateTime? signDateTo = null)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";

            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";

            if (signDateFrom.HasValue) url += $"&signDateFrom={signDateFrom.Value:yyyy-MM-dd}";
            if (signDateTo.HasValue) url += $"&signDateTo={signDateTo.Value:yyyy-MM-dd}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<WorkOrderExecutionSummaryDto>>>(url);
            return response ?? ApiResponse<PagedResult<WorkOrderExecutionSummaryDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<WorkOrderExecutionSummaryDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
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

    /// <summary>
    /// 获取工单执行看板聚合数据
    /// </summary>
    public async Task<ApiResponse<List<WorkOrderExecutionDashboardItem>>> GetDashboardSummaryAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<WorkOrderExecutionDashboardItem>>>($"{BaseUrl}/dashboard-summary");
            return response ?? ApiResponse<List<WorkOrderExecutionDashboardItem>>.Fail("获取看板数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<WorkOrderExecutionDashboardItem>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 全量刷新所有工单的执行状况汇总
    /// </summary>
    public async Task<ApiResponse<WorkOrderExecutionRefreshResultDto>> RefreshAllAsync()
    {
        try
        {
            var response = await _http.PostAsJsonAsync<object, ApiResponse<WorkOrderExecutionRefreshResultDto>>($"{BaseUrl}/refresh-all", new { });
            return response ?? ApiResponse<WorkOrderExecutionRefreshResultDto>.Fail("刷新失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<WorkOrderExecutionRefreshResultDto>.Fail($"网络错误: {ex.Message}");
        }
    }
}
