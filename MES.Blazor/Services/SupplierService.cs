using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class SupplierService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/supplier";

    public SupplierService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<SupplierProfileDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword)) url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
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
}
