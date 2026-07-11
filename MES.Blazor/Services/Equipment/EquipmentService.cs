using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Shared;

namespace MES.Blazor.Services;

public class EquipmentService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.Equipment;

    public EquipmentService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<EquipmentListDto>>> GetPagedAsync(EquipmentQueryParams query)
    {
        try
        {
            var isDescending = query.IsDescending ? "true" : "false";
            var encodedSortBy = Uri.EscapeDataString(query.SortBy ?? ApiEndpoints.DefaultSortBy);
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={encodedSortBy}&isDescending={isDescending}";
            if (!string.IsNullOrEmpty(query.Keyword)) url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (!string.IsNullOrEmpty(query.LifecycleStatus)) url += $"&lifecycleStatus={Uri.EscapeDataString(query.LifecycleStatus)}";
            if (!string.IsNullOrEmpty(query.UsageType)) url += $"&usageType={Uri.EscapeDataString(query.UsageType)}";
            if (!string.IsNullOrEmpty(query.RunningStatus)) url += $"&runningStatus={Uri.EscapeDataString(query.RunningStatus)}";
            if (!string.IsNullOrEmpty(query.InspectionStatus)) url += $"&inspectionStatus={Uri.EscapeDataString(query.InspectionStatus)}";
            if (!string.IsNullOrEmpty(query.MaintStatus)) url += $"&maintStatus={Uri.EscapeDataString(query.MaintStatus)}";
            if (!string.IsNullOrEmpty(query.Location)) url += $"&location={Uri.EscapeDataString(query.Location)}";
            if (!string.IsNullOrEmpty(query.RelatedSection)) url += $"&relatedSection={Uri.EscapeDataString(query.RelatedSection)}";
            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<EquipmentListDto>>>(url)
                   ?? ApiResponse<PagedResult<EquipmentListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<EquipmentListDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<EquipmentListDto>>> GetAllListAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<EquipmentListDto>>>($"{BaseUrl}/all-list")
                   ?? ApiResponse<List<EquipmentListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<EquipmentListDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts");
            return response ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex) { return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<EquipmentListDto>>> GetAllAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<EquipmentListDto>>>($"{BaseUrl}/all")
                   ?? ApiResponse<List<EquipmentListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<EquipmentListDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<EquipmentDetailDto>> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<EquipmentDetailDto>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<EquipmentDetailDto>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<EquipmentDetailDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<EquipmentDetailDto>> CreateAsync(CreateEquipmentRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateEquipmentRequest, ApiResponse<EquipmentDetailDto>>(BaseUrl, request)
                   ?? ApiResponse<EquipmentDetailDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<EquipmentDetailDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<EquipmentDetailDto>> UpdateAsync(int id, UpdateEquipmentRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateEquipmentRequest, ApiResponse<EquipmentDetailDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<EquipmentDetailDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<EquipmentDetailDto>.Fail($"网络错误: {ex.Message}"); }
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

    [Obsolete("请直接使用 openPdfFromApi 调用 -file 端点")]
    public async Task<ApiResponse<string>> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        try
        {
            var request = new EquipmentPrintBatchRequest { Ids = ids, Columns = columns };
            return await _http.PostAsJsonAsync<EquipmentPrintBatchRequest, ApiResponse<string>>($"{BaseUrl}/print-batch", request)
                   ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    [Obsolete("请直接使用 openPdfFromApi 调用 -file 端点")]
    public async Task<ApiResponse<string>> PrintAllAsync(EquipmentQueryParams query, List<PrintColumnDef> columns)
    {
        try
        {
            var request = new EquipmentPrintAllRequest
            {
                Keyword = query.Keyword,
                SortBy = query.SortBy,
                IsDescending = query.IsDescending,
                LifecycleStatus = query.LifecycleStatus,
                UsageType = query.UsageType,
                RunningStatus = query.RunningStatus,
                InspectionStatus = query.InspectionStatus,
                MaintStatus = query.MaintStatus,
                Location = query.Location,
                RelatedSection = query.RelatedSection,
                Columns = columns
            };
            return await _http.PostAsJsonAsync<EquipmentPrintAllRequest, ApiResponse<string>>($"{BaseUrl}/print-all", request)
                   ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }
}
