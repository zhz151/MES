using System.Text.Json;
using MES.Core.DTOs;
using MES.Shared.Constants;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 原锁计划前端服务
/// </summary>
public class RawMaterialLockPlanAndExecutionService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.RawMaterialLockPlan;

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

    public async Task<ApiResponse<SetPreExecuteFlagsResult>> SetPreExecuteFlagsAsync(List<int> workOrderIds, bool? isPreInput, bool? isMainNoMaterialComplete, DateTime? budgetInputDate = null, bool? isBudgetComplete = null)
    {
        try
        {
            var request = new { WorkOrderIds = workOrderIds, IsPreInput = isPreInput, IsMainNoMaterialComplete = isMainNoMaterialComplete, BudgetInputDate = budgetInputDate, IsBudgetComplete = isBudgetComplete };
            var response = await _http.PostAsJsonAsync<object, ApiResponse<SetPreExecuteFlagsResult>>($"{BaseUrl}/set-pre-execute-flags", request);
            return response ?? ApiResponse<SetPreExecuteFlagsResult>.Fail("设置预执行标记失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<SetPreExecuteFlagsResult>.Fail($"网络错误: {ex.Message}");
        }
    }

    [Obsolete("请直接使用 openPdfFromApi 调用 -file 端点")]
    public async Task<ApiResponse<string>> PrintAsync(RawMaterialLockPlanPrintRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<RawMaterialLockPlanPrintRequest, ApiResponse<string>>($"{BaseUrl}/print", request);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail($"网络错误: {ex.Message}");
        }
    }
}
