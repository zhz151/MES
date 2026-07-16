using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Shared;

namespace MES.Blazor.Services;

public class NcrService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.Ncr;

    public NcrService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<NcrDto>>> GetAllAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null, bool isDescending = true, string? filters = null, DateTime? reportDateFrom = null, DateTime? reportDateTo = null)
    {
        try
        {
            var url = $"{BaseUrl}/all?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (reportDateFrom.HasValue) url += $"&reportDateFrom={reportDateFrom.Value:yyyy-MM-dd}";
            if (reportDateTo.HasValue) url += $"&reportDateTo={reportDateTo.Value:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<NcrDto>>>(url)
                   ?? ApiResponse<PagedResult<NcrDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<NcrDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<NcrDto>> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<NcrDto>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<NcrDto>.Fail("获取详情失败");
        }
        catch (Exception ex) { return ApiResponse<NcrDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<NcrDto>> CreateAsync(CreateNcrRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateNcrRequest, ApiResponse<NcrDto>>(BaseUrl, request)
                   ?? ApiResponse<NcrDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<NcrDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<NcrDto>> UpdateAsync(int id, UpdateNcrRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateNcrRequest, ApiResponse<NcrDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<NcrDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<NcrDto>.Fail($"网络错误: {ex.Message}"); }
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

    public async Task<ApiResponse<NcrDto>> UpdateStatusAsync(int id, string status)
    {
        try
        {
            return await _http.PutAsJsonAsync<object, ApiResponse<NcrDto>>($"{BaseUrl}/{id}/status", new { Status = status })
                   ?? ApiResponse<NcrDto>.Fail("状态更新失败");
        }
        catch (Exception ex) { return ApiResponse<NcrDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<NcrLookupResultDto?>> LookupBatchAsync(string batchNo)
    {
        try
        {
            var url = $"{BaseUrl}/lookup-batch?batchNo={Uri.EscapeDataString(batchNo)}";
            return await _http.GetFromJsonAsync<ApiResponse<NcrLookupResultDto?>>(url)
                   ?? ApiResponse<NcrLookupResultDto?>.Ok(null, "查询成功");
        }
        catch (Exception ex) { return ApiResponse<NcrLookupResultDto?>.Ok(null, $"网络错误: {ex.Message}"); }
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

    public async Task<ApiResponse<List<NcrPendingCheckDto>>> GetPendingChecksAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<NcrPendingCheckDto>>>($"{BaseUrl}/pending-checks")
                   ?? ApiResponse<List<NcrPendingCheckDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<NcrPendingCheckDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 打印（HTML） ==========

    public async Task<ApiResponse<string>> PrintSelectedAsync(int[] ids, List<PrintColumnDef> columns)
    {
        try
        {
            var request = new { ids, columns };
            return await _http.PostAsJsonAsync<object, ApiResponse<string>>($"{BaseUrl}/print-selected-file", request)
                   ?? ApiResponse<string>.Fail("打印请求失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> PrintAllAsync(string? keyword, List<PrintColumnDef> columns)
    {
        try
        {
            var request = new { keyword, columns };
            return await _http.PostAsJsonAsync<object, ApiResponse<string>>($"{BaseUrl}/print-all-file", request)
                   ?? ApiResponse<string>.Fail("打印请求失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }
}
