using System.Text.Json;
using MES.Core.DTOs.Batch;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Blazor.Services;

/// <summary>委外单位档案（工段委外单位主档）前端服务</summary>
public class OutsourceVendorService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.OutsourceVendorProfile;

    public OutsourceVendorService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<OutsourceVendorProfileDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var isDescending = query.IsDescending ? "true" : "false";
            var encodedSortBy = Uri.EscapeDataString(query.SortBy ?? ApiEndpoints.DefaultSortBy);
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={encodedSortBy}&isDescending={isDescending}";
            if (!string.IsNullOrEmpty(query.Keyword)) url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<OutsourceVendorProfileDto>>>(url)
                   ?? ApiResponse<PagedResult<OutsourceVendorProfileDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<OutsourceVendorProfileDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<OutsourceVendorProfileDto>> CreateAsync(CreateOutsourceVendorRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateOutsourceVendorRequest, ApiResponse<OutsourceVendorProfileDto>>(BaseUrl, request)
                   ?? ApiResponse<OutsourceVendorProfileDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<OutsourceVendorProfileDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<OutsourceVendorProfileDto>>> CreateBatchAsync(List<CreateOutsourceVendorRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateOutsourceVendorRequest>, ApiResponse<List<OutsourceVendorProfileDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<OutsourceVendorProfileDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<OutsourceVendorProfileDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<OutsourceVendorProfileDto>> UpdateAsync(int id, UpdateOutsourceVendorRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateOutsourceVendorRequest, ApiResponse<OutsourceVendorProfileDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<OutsourceVendorProfileDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<OutsourceVendorProfileDto>.Fail($"网络错误: {ex.Message}"); }
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

    /// <summary>
    /// 获取启用中的委外单位档案全量（active，小表），供工段委外下拉按行工段过滤
    /// </summary>
    public async Task<ApiResponse<List<OutsourceVendorProfileDto>>> GetActiveAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<OutsourceVendorProfileDto>>>($"{BaseUrl}/active")
                   ?? ApiResponse<List<OutsourceVendorProfileDto>>.Fail("获取启用档案失败");
        }
        catch (Exception ex) { return ApiResponse<List<OutsourceVendorProfileDto>>.Fail($"网络错误: {ex.Message}"); }
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
