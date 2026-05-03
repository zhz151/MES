using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class SubcontractOrderService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/subcontract";

    public SubcontractOrderService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<SubcontractOrderDto>>> GetPagedAsync(QueryParams query, string? status = null, DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword)) url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (!string.IsNullOrEmpty(status)) url += $"&status={Uri.EscapeDataString(status)}";
            if (dateFrom.HasValue) url += $"&dateFrom={dateFrom.Value:yyyy-MM-dd}";
            if (dateTo.HasValue) url += $"&dateTo={dateTo.Value:yyyy-MM-dd}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<SubcontractOrderDto>>>(url)
                   ?? ApiResponse<PagedResult<SubcontractOrderDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<SubcontractOrderDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<SubcontractOrderDto>> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<SubcontractOrderDto>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<SubcontractOrderDto>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<SubcontractOrderDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<SubcontractOrderDto>> CreateAsync(CreateSubcontractOrderRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateSubcontractOrderRequest, ApiResponse<SubcontractOrderDto>>(BaseUrl, request)
                   ?? ApiResponse<SubcontractOrderDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<SubcontractOrderDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<SubcontractOrderDto>> UpdateAsync(int id, UpdateSubcontractOrderRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateSubcontractOrderRequest, ApiResponse<SubcontractOrderDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<SubcontractOrderDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<SubcontractOrderDto>.Fail($"网络错误: {ex.Message}"); }
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
