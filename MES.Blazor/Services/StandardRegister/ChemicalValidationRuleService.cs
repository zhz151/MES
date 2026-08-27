using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.StandardRegister;

namespace MES.Blazor.Services;

public class ChemicalValidationRuleService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.ChemicalValidationRule;

    public ChemicalValidationRuleService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<ChemicalValidationRuleDto>>> GetAllAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null, bool isDescending = true, List<FilterDescriptor>? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/all?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(filters))}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<ChemicalValidationRuleDto>>>(url)
                   ?? ApiResponse<PagedResult<ChemicalValidationRuleDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<ChemicalValidationRuleDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<ChemicalValidationRuleDto>>> BatchCreateAsync(List<CreateChemicalValidationRuleRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateChemicalValidationRuleRequest>, ApiResponse<List<ChemicalValidationRuleDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<ChemicalValidationRuleDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<ChemicalValidationRuleDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<ChemicalValidationRuleDto>> UpdateAsync(int id, UpdateChemicalValidationRuleRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateChemicalValidationRuleRequest, ApiResponse<ChemicalValidationRuleDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<ChemicalValidationRuleDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<ChemicalValidationRuleDto>.Fail($"网络错误: {ex.Message}"); }
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
