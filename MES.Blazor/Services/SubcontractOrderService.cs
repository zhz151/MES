using System.Text.Json;
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class SubcontractOrderService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/subcontract";

    public SubcontractOrderService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<List<SubcontractOrderDto>>> GetAllListAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<SubcontractOrderDto>>>($"{BaseUrl}/all")
                   ?? ApiResponse<List<SubcontractOrderDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<SubcontractOrderDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PagedResult<SubcontractOrderDto>>> GetPagedAsync(QueryParams query, string? status = null)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword)) url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (!string.IsNullOrEmpty(status)) url += $"&status={Uri.EscapeDataString(status)}";
            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
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

    /// <summary>
    /// 获取筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts")
                   ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex) { return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 打印 ==========

    public async Task<ApiResponse<string>> PrintOrderAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<string>>($"{BaseUrl}/{id}/print");
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> PrintOrderBatchAsync(int[] ids)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<OrderPrintBatchRequest, ApiResponse<string>>(
                $"{BaseUrl}/print-batch", new OrderPrintBatchRequest { Ids = ids });
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> PrintOrderAllAsync(string? keyword = null, string? sortBy = null, bool isDescending = false)
    {
        try
        {
            var request = new OrderPrintAllRequest { Keyword = keyword, SortBy = sortBy, IsDescending = isDescending };
            var response = await _http.PostAsJsonAsync<OrderPrintAllRequest, ApiResponse<string>>(
                $"{BaseUrl}/print-all", request);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }
}
