using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;

namespace MES.Blazor.Services;

public class SupplierService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.Supplier;

    public SupplierService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<List<SupplierProfileDto>>> GetAllListAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<SupplierProfileDto>>>($"{BaseUrl}/all")
                   ?? ApiResponse<List<SupplierProfileDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<SupplierProfileDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PagedResult<SupplierProfileDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var isDescending = query.IsDescending ? "true" : "false";
            var encodedSortBy = Uri.EscapeDataString(query.SortBy ?? ApiEndpoints.DefaultSortBy);
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={encodedSortBy}&isDescending={isDescending}";
            if (!string.IsNullOrEmpty(query.Keyword)) url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<SupplierProfileDto>>>(url)
                   ?? ApiResponse<PagedResult<SupplierProfileDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<SupplierProfileDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<SupplierProfileDto>> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<SupplierProfileDto>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<SupplierProfileDto>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<SupplierProfileDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<SupplierProfileDto>> CreateAsync(CreateSupplierRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateSupplierRequest, ApiResponse<SupplierProfileDto>>(BaseUrl, request)
                   ?? ApiResponse<SupplierProfileDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<SupplierProfileDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<SupplierProfileDto>>> CreateBatchAsync(List<CreateSupplierRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateSupplierRequest>, ApiResponse<List<SupplierProfileDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<SupplierProfileDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<SupplierProfileDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<SupplierProfileDto>> UpdateAsync(int id, UpdateSupplierRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateSupplierRequest, ApiResponse<SupplierProfileDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<SupplierProfileDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<SupplierProfileDto>.Fail($"网络错误: {ex.Message}"); }
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

    public async Task<List<SupplierProfileDto>> GetActiveAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<ApiResponse<List<SupplierProfileDto>>>($"{BaseUrl}/active");
            return result?.Data ?? new List<SupplierProfileDto>();
        }
        catch { return new List<SupplierProfileDto>(); }
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

}
