using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Shared;

namespace MES.Blazor.Services;

public class ProductionRecordService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.ProductionRecord;

    public ProductionRecordService(AuthHttpClient http) => _http = http;

    // ========== 内部生产记录 ==========

    public async Task<ApiResponse<ProductionRecordDto>> CreateProductionRecordAsync(CreateProductionRecordRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateProductionRecordRequest, ApiResponse<ProductionRecordDto>>($"{BaseUrl}/record", request)
                   ?? ApiResponse<ProductionRecordDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<ProductionRecordDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<ProductionRecordDto>>> BatchCreateProductionRecordsAsync(List<CreateProductionRecordRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateProductionRecordRequest>, ApiResponse<List<ProductionRecordDto>>>($"{BaseUrl}/records/batch", requests)
                   ?? ApiResponse<List<ProductionRecordDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<ProductionRecordDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<ProductionRecordDto>> UpdateProductionRecordAsync(int id, UpdateProductionRecordRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateProductionRecordRequest, ApiResponse<ProductionRecordDto>>($"{BaseUrl}/record/{id}", request)
                   ?? ApiResponse<ProductionRecordDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<ProductionRecordDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> DeleteProductionRecordAsync(int id)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/record/{id}")
                   ?? ApiResponse<object>.Fail("删除失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 工段委外 ==========

    // ========== 跨批次查询（用于独立页面） ==========

    public async Task<ApiResponse<PagedResult<ProductionRecordDto>>> GetAllProductionRecordsAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null, bool isDescending = true, DateTime? execDateFrom = null, DateTime? execDateTo = null, string? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/all/records?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (execDateFrom.HasValue) url += $"&execDateFrom={execDateFrom.Value:yyyy-MM-dd}";
            if (execDateTo.HasValue) url += $"&execDateTo={execDateTo.Value:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<ProductionRecordDto>>>(url)
                   ?? ApiResponse<PagedResult<ProductionRecordDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<ProductionRecordDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 筛选上下文 ==========

    /// <summary>
    /// 获取生产记录筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/all/filter-contexts")
                   ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex) { return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 批次跟踪字段 ==========

    /// <summary>
    /// 获取批次跟踪可视化数据
    /// </summary>
    public async Task<ApiResponse<BatchTrackingVisualDto>> GetTrackingVisualAsync(int batchId)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<BatchTrackingVisualDto>>($"{ApiEndpoints.Batch}/{batchId}/tracking")
                   ?? ApiResponse<BatchTrackingVisualDto>.Fail("获取跟踪数据失败");
        }
        catch (Exception ex) { return ApiResponse<BatchTrackingVisualDto>.Fail($"网络错误: {ex.Message}"); }
    }

}
