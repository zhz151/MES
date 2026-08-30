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
            if (query.LifecycleStatus.HasValue) url += $"&lifecycleStatus={Uri.EscapeDataString(query.LifecycleStatus.Value.ToString())}";
            if (query.UsageType.HasValue) url += $"&usageType={Uri.EscapeDataString(query.UsageType.Value.ToString())}";
            if (query.RunningStatus.HasValue) url += $"&runningStatus={Uri.EscapeDataString(query.RunningStatus.Value.ToString())}";
            if (query.InspectionStatus.HasValue) url += $"&inspectionStatus={Uri.EscapeDataString(query.InspectionStatus.Value.ToString())}";
            if (query.MaintStatus.HasValue) url += $"&maintStatus={Uri.EscapeDataString(query.MaintStatus.Value.ToString())}";
            if (!string.IsNullOrEmpty(query.Location)) url += $"&location={Uri.EscapeDataString(query.Location)}";
            if (!string.IsNullOrEmpty(query.RelatedSection)) url += $"&relatedSection={Uri.EscapeDataString(query.RelatedSection)}";
            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<EquipmentListDto>>>(url)
                   ?? ApiResponse<PagedResult<EquipmentListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<EquipmentListDto>>.Fail($"网络错误: {ex.Message}"); }
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

}
