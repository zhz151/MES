using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Shared;

namespace MES.Blazor.Services;

public class MaintenanceOrderService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.MaintenanceOrder;

    public MaintenanceOrderService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<MaintenanceOrderListDto>>> GetPagedAsync(MaintenanceOrderQueryParams query)
    {
        try
        {
            var isDescending = query.IsDescending ? "true" : "false";
            var encodedSortBy = Uri.EscapeDataString(query.SortBy ?? "Id");
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={encodedSortBy}&isDescending={isDescending}";
            if (!string.IsNullOrEmpty(query.Keyword)) url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<MaintenanceOrderListDto>>>(url)
                   ?? ApiResponse<PagedResult<MaintenanceOrderListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<MaintenanceOrderListDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<MaintenanceOrderListDto>>> CreateBatchAsync(List<CreateMaintenanceOrderRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateMaintenanceOrderRequest>, ApiResponse<List<MaintenanceOrderListDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<MaintenanceOrderListDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<MaintenanceOrderListDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<MaintenanceOrderListDto>> UpdateAsync(int id, UpdateMaintenanceRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateMaintenanceRequest, ApiResponse<MaintenanceOrderListDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<MaintenanceOrderListDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<MaintenanceOrderListDto>.Fail($"网络错误: {ex.Message}"); }
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

public async Task<ApiResponse<PagedResult<MaintenanceOrderListDto>>> GetPagedAsync(int pageIndex, int pageSize, string? keyword, string? sortBy, bool isDescending, string? filters)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={pageIndex}&pageSize={pageSize}&sortBy={Uri.EscapeDataString(sortBy ?? "Id")}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<MaintenanceOrderListDto>>>(url)
                   ?? ApiResponse<PagedResult<MaintenanceOrderListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<MaintenanceOrderListDto>>.Fail($"网络错误: {ex.Message}"); }
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
}
