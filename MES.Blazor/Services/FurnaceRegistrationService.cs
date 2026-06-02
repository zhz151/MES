using System.Text.Json;
using MES.Core.DTOs;
using MES.Shared.Constants;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class FurnaceRegistrationService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.FurnaceRegistration;

    public FurnaceRegistrationService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<FurnaceRegistrationDto>> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<FurnaceRegistrationDto>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<FurnaceRegistrationDto>.Fail("获取详情失败");
        }
        catch (Exception ex) { return ApiResponse<FurnaceRegistrationDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<FurnaceRegistrationDto>>> GetAllListAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<FurnaceRegistrationDto>>>($"{BaseUrl}/all-list")
                   ?? ApiResponse<List<FurnaceRegistrationDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<FurnaceRegistrationDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PagedResult<FurnaceRegistrationDto>>> GetAllAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null, bool isDescending = true, List<FilterDescriptor>? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/all?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(filters))}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<FurnaceRegistrationDto>>>(url)
                   ?? ApiResponse<PagedResult<FurnaceRegistrationDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<FurnaceRegistrationDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<FurnaceRegistrationDto>>> BatchCreateAsync(List<CreateFurnaceRegistrationRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateFurnaceRegistrationRequest>, ApiResponse<List<FurnaceRegistrationDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<FurnaceRegistrationDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<FurnaceRegistrationDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<FurnaceRegistrationDto>> UpdateAsync(int id, UpdateFurnaceRegistrationRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateFurnaceRegistrationRequest, ApiResponse<FurnaceRegistrationDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<FurnaceRegistrationDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<FurnaceRegistrationDto>.Fail($"网络错误: {ex.Message}"); }
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

    public async Task<ApiResponse<string?>> LookupPlantGradeAsync(string registeredGrade)
    {
        try
        {
            var url = $"{BaseUrl}/lookup-grade?registeredGrade={Uri.EscapeDataString(registeredGrade)}";
            return await _http.GetFromJsonAsync<ApiResponse<string?>>(url)
                   ?? ApiResponse<string?>.Ok(null, "查询成功");
        }
        catch (Exception ex) { return ApiResponse<string?>.Ok(null, $"网络错误: {ex.Message}"); }
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
