using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;

namespace MES.Blazor.Services;

public class PurchaseOrderService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.PurchaseOrder;

    public PurchaseOrderService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<List<PurchaseOrderDto>>> GetAllListAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<PurchaseOrderDto>>>($"{BaseUrl}/all")
                   ?? ApiResponse<List<PurchaseOrderDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<PurchaseOrderDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PagedResult<PurchaseOrderDto>>> GetPagedAsync(QueryParams query, string? status = null, DateTime? dateFrom = null, DateTime? dateTo = null, DateTime? requiredDateFrom = null, DateTime? requiredDateTo = null)
    {
        try
        {
            var isDescending = query.IsDescending ? "true" : "false";
            var encodedSortBy = Uri.EscapeDataString(query.SortBy ?? "orderdate");
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={encodedSortBy}&isDescending={isDescending}";
            if (!string.IsNullOrEmpty(query.Keyword)) url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (!string.IsNullOrEmpty(status)) url += $"&status={Uri.EscapeDataString(status)}";
            if (dateFrom.HasValue) url += $"&dateFrom={dateFrom.Value:yyyy-MM-dd}";
            if (dateTo.HasValue) url += $"&dateTo={dateTo.Value:yyyy-MM-dd}";
            if (requiredDateFrom.HasValue) url += $"&requiredDateFrom={requiredDateFrom.Value:yyyy-MM-dd}";
            if (requiredDateTo.HasValue) url += $"&requiredDateTo={requiredDateTo.Value:yyyy-MM-dd}";
            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<PurchaseOrderDto>>>(url)
                   ?? ApiResponse<PagedResult<PurchaseOrderDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<PurchaseOrderDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PurchaseOrderDto>> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<PurchaseOrderDto>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<PurchaseOrderDto>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PurchaseOrderDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PurchaseOrderDto>> CreateAsync(CreatePurchaseOrderRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreatePurchaseOrderRequest, ApiResponse<PurchaseOrderDto>>(BaseUrl, request)
                   ?? ApiResponse<PurchaseOrderDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<PurchaseOrderDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<PurchaseOrderDto>>> CreateBatchAsync(List<CreatePurchaseOrderRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreatePurchaseOrderRequest>, ApiResponse<List<PurchaseOrderDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<PurchaseOrderDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<PurchaseOrderDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PurchaseOrderDto>> UpdateAsync(int id, UpdatePurchaseOrderRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdatePurchaseOrderRequest, ApiResponse<PurchaseOrderDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<PurchaseOrderDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<PurchaseOrderDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts")
                   ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex) { return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> SyncAllAsync()
    {
        try
        {
            return await _http.PostAsJsonAsync<object, ApiResponse<object>>($"{BaseUrl}/sync-all", new { })
                   ?? ApiResponse<object>.Fail("同步失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> SyncSingleAsync(int id)
    {
        try
        {
            return await _http.PostAsJsonAsync<object, ApiResponse<object>>($"{BaseUrl}/{id}/sync", new { })
                   ?? ApiResponse<object>.Fail("同步失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> UpdateStatusAsync(int id, UpdateOrderStatusRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateOrderStatusRequest, ApiResponse<object>>($"{BaseUrl}/{id}/status", request)
                   ?? ApiResponse<object>.Fail("状态更新失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> DeleteAsync(int id)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<object>.Fail("删除失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<ProcurementStatusDto>>> GetProcurementStatusAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<ProcurementStatusDto>>>($"{BaseUrl}/procurement-status")
                   ?? ApiResponse<List<ProcurementStatusDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<ProcurementStatusDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<OrderMismatchInfo>>> GetMismatchedOrdersAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<OrderMismatchInfo>>>($"{BaseUrl}/mismatched-orders")
                   ?? ApiResponse<List<OrderMismatchInfo>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<OrderMismatchInfo>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PlanDetailDto>> GetPlanDetailAsync(string workOrderNo, string materialCategory)
    {
        try
        {
            var url = $"{BaseUrl}/plan-detail?workOrderNo={Uri.EscapeDataString(workOrderNo)}&materialCategory={Uri.EscapeDataString(materialCategory)}";
            return await _http.GetFromJsonAsync<ApiResponse<PlanDetailDto>>(url)
                   ?? ApiResponse<PlanDetailDto>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PlanDetailDto>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 采购首页汇总（荒管/成品，isFinished=true 成品） ==========

    public async Task<ApiResponse<List<PurchasePendingDto>>> GetPurchasePendingAsync(bool isFinished)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<PurchasePendingDto>>>($"{BaseUrl}/summary/pending?isFinished={isFinished}")
                   ?? ApiResponse<List<PurchasePendingDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<PurchasePendingDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PurchaseInProgressResultDto>> GetPurchaseInProgressAsync(bool isFinished)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<PurchaseInProgressResultDto>>($"{BaseUrl}/summary/in-progress?isFinished={isFinished}")
                   ?? ApiResponse<PurchaseInProgressResultDto>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PurchaseInProgressResultDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PurchaseMonthlyResultDto>> GetPurchaseMonthlyAsync(bool isFinished)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<PurchaseMonthlyResultDto>>($"{BaseUrl}/summary/monthly?isFinished={isFinished}")
                   ?? ApiResponse<PurchaseMonthlyResultDto>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PurchaseMonthlyResultDto>.Fail($"网络错误: {ex.Message}"); }
    }

}
