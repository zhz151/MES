using MES.Core.DTOs;
using MES.Shared.Constants;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class HardnessTestService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.HardnessTest;

    public HardnessTestService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<HardnessTestDto>>> GetAllAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null, bool isDescending = true, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null, string? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/all?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (inspectionDateFrom.HasValue) url += $"&inspectionDateFrom={inspectionDateFrom.Value:yyyy-MM-dd}";
            if (inspectionDateTo.HasValue) url += $"&inspectionDateTo={inspectionDateTo.Value:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<HardnessTestDto>>>(url)
                   ?? ApiResponse<PagedResult<HardnessTestDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<HardnessTestDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<HardnessTestDto>> UpdateAsync(int id, UpdateHardnessTestRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateHardnessTestRequest, ApiResponse<HardnessTestDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<HardnessTestDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<HardnessTestDto>.Fail($"网络错误: {ex.Message}"); }
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

    public async Task<ApiResponse<List<HardnessTestDto>>> BatchCreateAsync(List<CreateHardnessTestRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateHardnessTestRequest>, ApiResponse<List<HardnessTestDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<HardnessTestDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<HardnessTestDto>>.Fail($"网络错误: {ex.Message}"); }
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
