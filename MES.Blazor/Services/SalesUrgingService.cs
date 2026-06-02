using System.Text.Json;
using MES.Core.DTOs;
using MES.Shared.Constants;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 销售催单前端服务
/// </summary>
public class SalesUrgingService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.SalesUrging;

    public SalesUrgingService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<SalesUrgingDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";

            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<SalesUrgingDto>>>(url);
            return response ?? ApiResponse<PagedResult<SalesUrgingDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<SalesUrgingDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveUrgingAsync(int workOrderId, bool isSalesUrging, string? urgingRemark)
    {
        try
        {
            var payload = new { WorkOrderId = workOrderId, IsSalesUrging = isSalesUrging, UrgingRemark = urgingRemark };
            var response = await _http.PostAsJsonAsync<object, ApiResponse<bool>>($"{BaseUrl}/save", payload);
            return response ?? ApiResponse<bool>.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveLockConfirmationAsync(int workOrderId, DateTime? estimatedArrivalDate, bool isMainNoMaterialComplete)
    {
        try
        {
            var payload = new { WorkOrderId = workOrderId, EstimatedArrivalDate = estimatedArrivalDate, IsMainNoMaterialComplete = isMainNoMaterialComplete };
            var response = await _http.PostAsJsonAsync<object, ApiResponse<bool>>($"{BaseUrl}/save-lock-confirmation", payload);
            return response ?? ApiResponse<bool>.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> UnlockAsync(int workOrderId)
    {
        try
        {
            var payload = new { WorkOrderId = workOrderId };
            var response = await _http.PostAsJsonAsync<object, ApiResponse<bool>>($"{BaseUrl}/unlock", payload);
            return response ?? ApiResponse<bool>.Fail("解锁失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
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
}
