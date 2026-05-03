using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class PurchaseOrderService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/purchase-order";

    public PurchaseOrderService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<PurchaseOrderDto>>> GetPagedAsync(QueryParams query, string? status = null, DateTime? dateFrom = null, DateTime? dateTo = null, DateTime? requiredDateFrom = null, DateTime? requiredDateTo = null)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword)) url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (!string.IsNullOrEmpty(status)) url += $"&status={Uri.EscapeDataString(status)}";
            if (dateFrom.HasValue) url += $"&dateFrom={dateFrom.Value:yyyy-MM-dd}";
            if (dateTo.HasValue) url += $"&dateTo={dateTo.Value:yyyy-MM-dd}";
            if (requiredDateFrom.HasValue) url += $"&requiredDateFrom={requiredDateFrom.Value:yyyy-MM-dd}";
            if (requiredDateTo.HasValue) url += $"&requiredDateTo={requiredDateTo.Value:yyyy-MM-dd}";
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

    public async Task<ApiResponse<PurchaseOrderDto>> UpdateAsync(int id, UpdatePurchaseOrderRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdatePurchaseOrderRequest, ApiResponse<PurchaseOrderDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<PurchaseOrderDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<PurchaseOrderDto>.Fail($"网络错误: {ex.Message}"); }
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
}
