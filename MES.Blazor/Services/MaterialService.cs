using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class MaterialService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/material";

    public MaterialService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<MaterialDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword)) url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<MaterialDto>>>(url)
                   ?? ApiResponse<PagedResult<MaterialDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<MaterialDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<MaterialDto>> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<MaterialDto>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<MaterialDto>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<MaterialDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<MaterialDto>> CreateAsync(CreateMaterialRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateMaterialRequest, ApiResponse<MaterialDto>>(BaseUrl, request)
                   ?? ApiResponse<MaterialDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<MaterialDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<MaterialDto>> UpdateAsync(int id, UpdateMaterialRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateMaterialRequest, ApiResponse<MaterialDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<MaterialDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<MaterialDto>.Fail($"网络错误: {ex.Message}"); }
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

    public async Task<List<string>> GetCategoriesAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<ApiResponse<List<string>>>($"{BaseUrl}/categories");
            return result?.Data ?? new List<string>();
        }
        catch { return new List<string>(); }
    }
}
