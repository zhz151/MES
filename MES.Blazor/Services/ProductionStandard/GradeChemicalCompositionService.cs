using System.Text.Json;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.ProductionStandard;

namespace MES.Blazor.Services;

public class GradeChemicalCompositionService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.GradeChemicalComposition;

    public GradeChemicalCompositionService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<GradeChemicalCompositionDto>> CreateAsync(CreateGradeChemicalCompositionRequest request)
    {
        try { return await _http.PostAsJsonAsync<CreateGradeChemicalCompositionRequest, ApiResponse<GradeChemicalCompositionDto>>(BaseUrl, request) ?? ApiResponse<GradeChemicalCompositionDto>.Fail("创建失败"); }
        catch (Exception ex) { return ApiResponse<GradeChemicalCompositionDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PagedResult<GradeChemicalCompositionDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword)) url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<GradeChemicalCompositionDto>>>(url)
                   ?? ApiResponse<PagedResult<GradeChemicalCompositionDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<GradeChemicalCompositionDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<GradeChemicalCompositionDto>> UpdateAsync(int id, UpdateGradeChemicalCompositionRequest request)
    {
        try { return await _http.PutAsJsonAsync<UpdateGradeChemicalCompositionRequest, ApiResponse<GradeChemicalCompositionDto>>($"{BaseUrl}/{id}", request) ?? ApiResponse<GradeChemicalCompositionDto>.Fail("更新失败"); }
        catch (Exception ex) { return ApiResponse<GradeChemicalCompositionDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> DeleteAsync(int id)
    {
        try { return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/{id}") ?? ApiResponse<object>.Fail("删除失败"); }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try { return await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts") ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败"); }
        catch (Exception ex) { return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}"); }
    }
}
