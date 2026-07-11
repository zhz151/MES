using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.WorkOrder;

namespace MES.Blazor.Services;

/// <summary>
/// 工单需求调整前端服务
/// </summary>
public class OrderDemandAdjustmentService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.OrderDemandAdjustment;

    public OrderDemandAdjustmentService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<OrderDemandAdjustmentDto>>> GetPagedAsync(QueryParams query, DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";

            if (dateFrom.HasValue) url += $"&signDateFrom={dateFrom.Value:yyyy-MM-dd}";
            if (dateTo.HasValue) url += $"&signDateTo={dateTo.Value:yyyy-MM-dd}";

            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<OrderDemandAdjustmentDto>>>(url);
            return response ?? ApiResponse<PagedResult<OrderDemandAdjustmentDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<OrderDemandAdjustmentDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveUrgingAsync(int workOrderId, bool isUrging, bool isBatchDelivery, bool isPaused, string? adjustmentRemark)
    {
        try
        {
            var payload = new { WorkOrderId = workOrderId, IsUrging = isUrging, IsBatchDelivery = isBatchDelivery, IsPaused = isPaused, AdjustmentRemark = adjustmentRemark };
            var response = await _http.PostAsJsonAsync<object, ApiResponse<bool>>($"{BaseUrl}/save", payload);
            return response ?? ApiResponse<bool>.Fail("保存失败");
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
