using MES.Core.Models;
using MES.Core.DTOs.Quality;

namespace MES.Blazor.Services;

public class PittingCorrosionTestService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/pitting-corrosion-test";

    public PittingCorrosionTestService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<PittingCorrosionTestDto>>> GetAllAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null, bool isDescending = true, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null, string? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/all?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (inspectionDateFrom.HasValue) url += $"&inspectionDateFrom={inspectionDateFrom.Value:yyyy-MM-dd}";
            if (inspectionDateTo.HasValue) url += $"&inspectionDateTo={inspectionDateTo.Value:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<PittingCorrosionTestDto>>>(url)
                   ?? ApiResponse<PagedResult<PittingCorrosionTestDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<PittingCorrosionTestDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PittingCorrosionTestDto>> UpdateAsync(int id, UpdatePittingCorrosionTestRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdatePittingCorrosionTestRequest, ApiResponse<PittingCorrosionTestDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<PittingCorrosionTestDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<PittingCorrosionTestDto>.Fail($"网络错误: {ex.Message}"); }
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

    public async Task<ApiResponse<List<PittingCorrosionTestDto>>> BatchCreateAsync(List<CreatePittingCorrosionTestRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreatePittingCorrosionTestRequest>, ApiResponse<List<PittingCorrosionTestDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<PittingCorrosionTestDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<PittingCorrosionTestDto>>.Fail($"网络错误: {ex.Message}"); }
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
