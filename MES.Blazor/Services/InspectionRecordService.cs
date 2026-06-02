using System.Text.Json;
using MES.Core.DTOs;
using MES.Shared.Constants;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class InspectionRecordService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.InspectionRecord;

    public InspectionRecordService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<InspectionRecordListDto>>> GetPagedAsync(InspectionRecordQueryParams query)
    {
        try
        {
            var isDescending = query.IsDescending ? "true" : "false";
            var encodedSortBy = Uri.EscapeDataString(query.SortBy ?? "Id");
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={encodedSortBy}&isDescending={isDescending}";
            if (!string.IsNullOrEmpty(query.Keyword)) url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<InspectionRecordListDto>>>(url)
                   ?? ApiResponse<PagedResult<InspectionRecordListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<InspectionRecordListDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<InspectionRecordListDto>>> GetAllListAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<InspectionRecordListDto>>>($"{BaseUrl}/all-list")
                   ?? ApiResponse<List<InspectionRecordListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<InspectionRecordListDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<InspectionRecordListDto>> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<InspectionRecordListDto>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<InspectionRecordListDto>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<InspectionRecordListDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<InspectionRecordListDto>> CreateAsync(CreateInspectionRecordRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateInspectionRecordRequest, ApiResponse<InspectionRecordListDto>>(BaseUrl, request)
                   ?? ApiResponse<InspectionRecordListDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<InspectionRecordListDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<InspectionRecordListDto>>> CreateBatchAsync(List<CreateInspectionRecordRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateInspectionRecordRequest>, ApiResponse<List<InspectionRecordListDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<InspectionRecordListDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<InspectionRecordListDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<InspectionRecordListDto>> UpdateAsync(int id, UpdateInspectionRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateInspectionRequest, ApiResponse<InspectionRecordListDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<InspectionRecordListDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<InspectionRecordListDto>.Fail($"网络错误: {ex.Message}"); }
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

    public async Task<ApiResponse<string>> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        try
        {
            var request = new InspectionRecordPrintBatchRequest { Ids = ids, Columns = columns };
            return await _http.PostAsJsonAsync<InspectionRecordPrintBatchRequest, ApiResponse<string>>($"{BaseUrl}/print-batch", request)
                   ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> PrintAllAsync(InspectionRecordQueryParams query, List<PrintColumnDef> columns)
    {
        try
        {
            var request = new InspectionRecordPrintAllRequest
            {
                Keyword = query.Keyword,
                SortBy = query.SortBy,
                IsDescending = query.IsDescending,
                EquipmentId = query.EquipmentId,
                Columns = columns
            };
            return await _http.PostAsJsonAsync<InspectionRecordPrintAllRequest, ApiResponse<string>>($"{BaseUrl}/print-all", request)
                   ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PagedResult<InspectionRecordListDto>>> GetPagedAsync(int pageIndex, int pageSize, string? keyword, string? sortBy, bool isDescending, string? filters)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={pageIndex}&pageSize={pageSize}&sortBy={Uri.EscapeDataString(sortBy ?? "Id")}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<InspectionRecordListDto>>>(url)
                   ?? ApiResponse<PagedResult<InspectionRecordListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<InspectionRecordListDto>>.Fail($"网络错误: {ex.Message}"); }
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
