using System.Text.Json;
using MES.Core.DTOs;
using MES.Shared.Constants;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class FinalInspectionService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.FinalInspection;

    public FinalInspectionService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<FinalInspectionDto>>> GetAllAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null, bool isDescending = true, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null, string? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/all?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (inspectionDateFrom.HasValue) url += $"&inspectionDateFrom={inspectionDateFrom.Value:yyyy-MM-dd}";
            if (inspectionDateTo.HasValue) url += $"&inspectionDateTo={inspectionDateTo.Value:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<FinalInspectionDto>>>(url)
                   ?? ApiResponse<PagedResult<FinalInspectionDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<FinalInspectionDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<FinalInspectionDto>>> GetAllListAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<FinalInspectionDto>>>($"{BaseUrl}/all-list")
                   ?? ApiResponse<List<FinalInspectionDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<FinalInspectionDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<FinalInspectionDto>> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<FinalInspectionDto>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<FinalInspectionDto>.Fail("获取详情失败");
        }
        catch (Exception ex) { return ApiResponse<FinalInspectionDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<FinalInspectionDto>> CreateAsync(CreateFinalInspectionRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateFinalInspectionRequest, ApiResponse<FinalInspectionDto>>(BaseUrl, request)
                   ?? ApiResponse<FinalInspectionDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<FinalInspectionDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<FinalInspectionDto>> UpdateAsync(int id, UpdateFinalInspectionRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateFinalInspectionRequest, ApiResponse<FinalInspectionDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<FinalInspectionDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<FinalInspectionDto>.Fail($"网络错误: {ex.Message}"); }
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

    public async Task<ApiResponse<List<FinalInspectionDto>>> BatchCreateAsync(List<CreateFinalInspectionRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateFinalInspectionRequest>, ApiResponse<List<FinalInspectionDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<FinalInspectionDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<FinalInspectionDto>>.Fail($"网络错误: {ex.Message}"); }
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

    public async Task<ApiResponse<BatchLookupResultDto?>> LookupBatchAsync(string batchNo)
    {
        try
        {
            var url = $"{BaseUrl}/lookup-batch?batchNo={Uri.EscapeDataString(batchNo)}";
            return await _http.GetFromJsonAsync<ApiResponse<BatchLookupResultDto?>>(url)
                   ?? ApiResponse<BatchLookupResultDto?>.Ok(null, "查询成功");
        }
        catch (Exception ex) { return ApiResponse<BatchLookupResultDto?>.Ok(null, $"网络错误: {ex.Message}"); }
    }
}
