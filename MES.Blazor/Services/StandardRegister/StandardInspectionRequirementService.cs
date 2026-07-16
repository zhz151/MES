using MES.Blazor.Services;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.StandardRegister;

namespace MES.Blazor.Services;

public class StandardInspectionRequirementService
{
    private readonly AuthHttpClient _http;
    private readonly string BaseUrl = ApiEndpoints.StandardInspectionRequirement;

    public StandardInspectionRequirementService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<StandardInspectionRequirementDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}";
            if (!string.IsNullOrWhiteSpace(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (!string.IsNullOrWhiteSpace(query.SortBy))
                url += $"&sortBy={query.SortBy}";
            url += $"&isDescending={query.IsDescending}";
            if (query.Filters?.Count > 0)
                url += $"&filters={Uri.EscapeDataString(System.Text.Json.JsonSerializer.Serialize(query.Filters))}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<StandardInspectionRequirementDto>>>(url);
            return response ?? ApiResponse<PagedResult<StandardInspectionRequirementDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<StandardInspectionRequirementDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<StandardInspectionRequirementDto>> CreateAsync(CreateStandardInspectionRequirementRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateStandardInspectionRequirementRequest, ApiResponse<StandardInspectionRequirementDto>>(BaseUrl, request)
                   ?? ApiResponse<StandardInspectionRequirementDto>.Fail("创建失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<StandardInspectionRequirementDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse> UpdateAsync(int id, UpdateStandardInspectionRequirementRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<UpdateStandardInspectionRequirementRequest, ApiResponse<StandardInspectionRequirementDto>>($"{BaseUrl}/{id}", request);
            return response?.Success == true
                ? ApiResponse.Ok("更新成功")
                : ApiResponse.Fail(response?.Message ?? "更新失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse> DeleteAsync(int id)
    {
        try
        {
            var response = await _http.DeleteFromJsonAsync<ApiResponse>($"{BaseUrl}/{id}");
            return response ?? ApiResponse.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts");
            return response ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选条件失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}");
        }
    }
}
