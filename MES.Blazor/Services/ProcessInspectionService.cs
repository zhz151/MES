using System.Text.Json;
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class ProcessInspectionService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/process-inspection";

    public ProcessInspectionService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<ProcessInspectionDto>>> GetAllAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null, bool isDescending = true, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null, string? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/all?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (inspectionDateFrom.HasValue) url += $"&inspectionDateFrom={inspectionDateFrom.Value:yyyy-MM-dd}";
            if (inspectionDateTo.HasValue) url += $"&inspectionDateTo={inspectionDateTo.Value:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<ProcessInspectionDto>>>(url)
                   ?? ApiResponse<PagedResult<ProcessInspectionDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<ProcessInspectionDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<ProcessInspectionDto>>> GetAllListAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<ProcessInspectionDto>>>($"{BaseUrl}/all-list")
                   ?? ApiResponse<List<ProcessInspectionDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<ProcessInspectionDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<ProcessInspectionDto>>> BatchCreateAsync(List<CreateProcessInspectionRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateProcessInspectionRequest>, ApiResponse<List<ProcessInspectionDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<ProcessInspectionDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<ProcessInspectionDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<ProcessInspectionDto>> UpdateAsync(int id, UpdateProcessInspectionRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateProcessInspectionRequest, ApiResponse<ProcessInspectionDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<ProcessInspectionDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<ProcessInspectionDto>.Fail($"网络错误: {ex.Message}"); }
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
}
