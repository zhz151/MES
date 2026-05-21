using System.Text.Json;
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 工单执行状况前端服务
/// </summary>
public class WorkOrderExecutionService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/workorder-execution";

    public WorkOrderExecutionService(AuthHttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// 分页查询工单执行状况列表
    /// </summary>
    public async Task<ApiResponse<PagedResult<WorkOrderExecutionSummaryDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";

            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<WorkOrderExecutionSummaryDto>>>(url);
            return response ?? ApiResponse<PagedResult<WorkOrderExecutionSummaryDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<WorkOrderExecutionSummaryDto>>.Fail($"网络错误: {ex.Message}");
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
