using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Batch;

namespace MES.Blazor.Services;

public class PicklingService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.Pickling;

    public PicklingService(AuthHttpClient http) => _http = http;

    // ========== 入缸记录 ==========

    public async Task<ApiResponse<PagedResult<PicklingInRecordDto>>> GetPagedAsync(
        int pageIndex = 1, int pageSize = 20, string? keyword = null,
        string? sortBy = null, bool isDescending = true,
        DateTime? inDateFrom = null, DateTime? inDateTo = null,
        DateTime? completeDateFrom = null, DateTime? completeDateTo = null,
        string? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (inDateFrom.HasValue) url += $"&inDateFrom={inDateFrom.Value:yyyy-MM-dd}";
            if (inDateTo.HasValue) url += $"&inDateTo={inDateTo.Value:yyyy-MM-dd}";
            if (completeDateFrom.HasValue) url += $"&completeDateFrom={completeDateFrom.Value:yyyy-MM-dd}";
            if (completeDateTo.HasValue) url += $"&completeDateTo={completeDateTo.Value:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<PicklingInRecordDto>>>(url)
                   ?? ApiResponse<PagedResult<PicklingInRecordDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<PicklingInRecordDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PicklingInRecordDto>> CreateAsync(CreatePicklingInRecordRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreatePicklingInRecordRequest, ApiResponse<PicklingInRecordDto>>(BaseUrl, request)
                   ?? ApiResponse<PicklingInRecordDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<PicklingInRecordDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<PicklingInRecordDto>>> BatchCreateAsync(List<CreatePicklingInRecordRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreatePicklingInRecordRequest>, ApiResponse<List<PicklingInRecordDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<PicklingInRecordDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<PicklingInRecordDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PicklingInRecordDto>> UpdateAsync(int id, UpdatePicklingInRecordRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdatePicklingInRecordRequest, ApiResponse<PicklingInRecordDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<PicklingInRecordDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<PicklingInRecordDto>.Fail($"网络错误: {ex.Message}"); }
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

    // ========== 完工记录 ==========

    public async Task<ApiResponse<PicklingOutRecordDto?>> GetOutRecordByInIdAsync(int picklingInRecordId)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<PicklingOutRecordDto?>>($"{BaseUrl}/{picklingInRecordId}/out-record")
                   ?? ApiResponse<PicklingOutRecordDto?>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PicklingOutRecordDto?>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PagedResult<PicklingOutRecordDto>>> GetOutRecordsPagedAsync(
        int pageIndex = 1, int pageSize = 20, string? keyword = null,
        string? sortBy = null, bool isDescending = true,
        DateTime? completeDateFrom = null, DateTime? completeDateTo = null,
        string? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/out-records/list?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (completeDateFrom.HasValue) url += $"&completeDateFrom={completeDateFrom.Value:yyyy-MM-dd}";
            if (completeDateTo.HasValue) url += $"&completeDateTo={completeDateTo.Value:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<PicklingOutRecordDto>>>(url)
                   ?? ApiResponse<PagedResult<PicklingOutRecordDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<PicklingOutRecordDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PicklingOutRecordDto>> CreateOutRecordAsync(CreatePicklingOutRecordRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreatePicklingOutRecordRequest, ApiResponse<PicklingOutRecordDto>>($"{BaseUrl}/out-record", request)
                   ?? ApiResponse<PicklingOutRecordDto>.Fail("创建完工记录失败");
        }
        catch (Exception ex) { return ApiResponse<PicklingOutRecordDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PicklingOutRecordDto>> UpdateOutRecordAsync(int id, UpdatePicklingOutRecordRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdatePicklingOutRecordRequest, ApiResponse<PicklingOutRecordDto>>($"{BaseUrl}/out-record/{id}", request)
                   ?? ApiResponse<PicklingOutRecordDto>.Fail("更新完工记录失败");
        }
        catch (Exception ex) { return ApiResponse<PicklingOutRecordDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> DeleteOutRecordAsync(int id)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/out-record/{id}")
                   ?? ApiResponse<object>.Fail("删除完工记录失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 筛选上下文 ==========

    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts");
            return response ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetOutRecordFilterContextsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/out-records/filter-contexts");
            return response ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}");
        }
    }

    // ========== 按批次查询 ==========

    public async Task<ApiResponse<List<PicklingInRecordDto>>> GetByBatchAsync(string batchNo)
    {
        try
        {
            var url = $"{BaseUrl}/by-batch/{Uri.EscapeDataString(batchNo)}";
            return await _http.GetFromJsonAsync<ApiResponse<List<PicklingInRecordDto>>>(url)
                   ?? ApiResponse<List<PicklingInRecordDto>>.Fail("请求失败");
        }
        catch (Exception ex) { return ApiResponse<List<PicklingInRecordDto>>.Fail($"网络错误: {ex.Message}"); }
    }
}
