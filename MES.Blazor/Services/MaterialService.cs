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
            var isDescending = query.IsDescending ? "true" : "false";
            var encodedSortBy = Uri.EscapeDataString(query.SortBy ?? "CreatedTime");
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={encodedSortBy}&isDescending={isDescending}";
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

    public async Task<ApiResponse<List<MaterialDto>>> CreateBatchAsync(List<CreateMaterialRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateMaterialRequest>, ApiResponse<List<MaterialDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<MaterialDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<MaterialDto>>.Fail($"网络错误: {ex.Message}"); }
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

    /// <summary>
    /// 匹配物料（按分类+钢种+规格）
    /// </summary>
    public async Task<ApiResponse<MaterialDto?>> MatchAsync(string category, string grade, string spec)
    {
        try
        {
            var url = $"{BaseUrl}/match?category={Uri.EscapeDataString(category)}&grade={Uri.EscapeDataString(grade)}&spec={Uri.EscapeDataString(spec)}";
            var response = await _http.GetFromJsonAsync<ApiResponse<MaterialDto?>>(url);
            return response ?? ApiResponse<MaterialDto?>.Ok(null, "查询失败");
        }
        catch (Exception ex) { return ApiResponse<MaterialDto?>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>
    /// 批量匹配物料，返回不存在的物料列表
    /// </summary>
    public async Task<ApiResponse<List<BatchMaterialMatchItem>>> BatchMatchAsync(List<BatchMaterialMatchItem> items)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<List<BatchMaterialMatchItem>, ApiResponse<List<BatchMaterialMatchItem>>>(
                $"{BaseUrl}/batch-match", items);
            return response ?? ApiResponse<List<BatchMaterialMatchItem>>.Ok(new List<BatchMaterialMatchItem>(), "查询失败");
        }
        catch (Exception ex) { return ApiResponse<List<BatchMaterialMatchItem>>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 打印 ==========

    public async Task<ApiResponse<string>> PrintMaterialAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<string>>($"{BaseUrl}/{id}/print");
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> PrintMaterialBatchAsync(int[] ids)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<OrderPrintBatchRequest, ApiResponse<string>>(
                $"{BaseUrl}/print-batch", new OrderPrintBatchRequest { Ids = ids });
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> PrintMaterialAllAsync(string? keyword = null, string? sortBy = null, bool isDescending = false)
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
