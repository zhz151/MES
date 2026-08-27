using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Quality;

namespace MES.Blazor.Services;

/// <summary>
/// 检验到料（成检到料）前端服务
/// </summary>
public class MaterialReceiveCheckService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.MaterialReceiveCheck;

    public MaterialReceiveCheckService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<MaterialReceiveCheckDto>> CreateMaterialReceiveCheckAsync(CreateMaterialReceiveCheckRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateMaterialReceiveCheckRequest, ApiResponse<MaterialReceiveCheckDto>>($"{BaseUrl}", request)
                   ?? ApiResponse<MaterialReceiveCheckDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<MaterialReceiveCheckDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<MaterialReceiveCheckDto>> UpdateMaterialReceiveCheckAsync(int id, UpdateMaterialReceiveCheckRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateMaterialReceiveCheckRequest, ApiResponse<MaterialReceiveCheckDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<MaterialReceiveCheckDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<MaterialReceiveCheckDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> DeleteMaterialReceiveCheckAsync(int id)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<object>.Fail("删除失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PagedResult<MaterialReceiveCheckDto>>> GetAllMaterialReceiveChecksAsync(
        int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null,
        bool isDescending = true, DateTime? receiveDateFrom = null, DateTime? receiveDateTo = null,
        string? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/all?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (receiveDateFrom.HasValue) url += $"&receiveDateFrom={receiveDateFrom.Value:yyyy-MM-dd}";
            if (receiveDateTo.HasValue) url += $"&receiveDateTo={receiveDateTo.Value:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<MaterialReceiveCheckDto>>>(url)
                   ?? ApiResponse<PagedResult<MaterialReceiveCheckDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<MaterialReceiveCheckDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<MaterialCheckHealthSummaryDto>> GetMaterialCheckHealthSummaryAsync(
        string? keyword = null, DateTime? receiveDateFrom = null, DateTime? receiveDateTo = null, string? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/health-summary";
            if (!string.IsNullOrEmpty(keyword)) url += $"?keyword={Uri.EscapeDataString(keyword)}";
            if (receiveDateFrom.HasValue) url += $"&receiveDateFrom={receiveDateFrom.Value:yyyy-MM-dd}";
            if (receiveDateTo.HasValue) url += $"&receiveDateTo={receiveDateTo.Value:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<MaterialCheckHealthSummaryDto>>(url)
                   ?? ApiResponse<MaterialCheckHealthSummaryDto>.Fail("获取健康汇总失败");
        }
        catch (Exception ex) { return ApiResponse<MaterialCheckHealthSummaryDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetMaterialCheckFilterContextsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts")
                   ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex) { return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<PendingMaterialCheckDto>>> GetPendingMaterialChecksAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<PendingMaterialCheckDto>>>($"{BaseUrl}/pending")
                   ?? ApiResponse<List<PendingMaterialCheckDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<PendingMaterialCheckDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<MaterialReceiveCheckDto>>> BatchCreateMaterialReceiveChecksAsync(
        List<CreateMaterialReceiveCheckRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateMaterialReceiveCheckRequest>, ApiResponse<List<MaterialReceiveCheckDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<MaterialReceiveCheckDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<MaterialReceiveCheckDto>>.Fail($"网络错误: {ex.Message}"); }
    }
}
