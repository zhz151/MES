using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class ProductionRecordService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/production-record";

    public ProductionRecordService(AuthHttpClient http) => _http = http;

    // ========== 内部生产记录 ==========

    public async Task<ApiResponse<PagedResult<ProductionRecordDto>>> GetProductionRecordsAsync(int batchId, int pageIndex = 1, int pageSize = 20)
    {
        try
        {
            var url = $"{BaseUrl}/{batchId}/records?pageIndex={pageIndex}&pageSize={pageSize}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<ProductionRecordDto>>>(url)
                   ?? ApiResponse<PagedResult<ProductionRecordDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<ProductionRecordDto>>.Fail($"网络错误: {ex.Message}"); }
    }

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

    public async Task<ApiResponse<PagedResult<SectionOutsourceDto>>> GetSectionOutsourcesAsync(int batchId, int pageIndex = 1, int pageSize = 20)
    {
        try
        {
            var url = $"{BaseUrl}/{batchId}/outsources?pageIndex={pageIndex}&pageSize={pageSize}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<SectionOutsourceDto>>>(url)
                   ?? ApiResponse<PagedResult<SectionOutsourceDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<SectionOutsourceDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<SectionOutsourceDto>> CreateSectionOutsourceAsync(CreateSectionOutsourceRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateSectionOutsourceRequest, ApiResponse<SectionOutsourceDto>>($"{BaseUrl}/outsource", request)
                   ?? ApiResponse<SectionOutsourceDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<SectionOutsourceDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> DeleteSectionOutsourceAsync(int id)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/outsource/{id}")
                   ?? ApiResponse<object>.Fail("删除失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 委外回收 ==========

    public async Task<ApiResponse<List<OutsourceRecoveryDto>>> GetOutsourceRecoveriesAsync(int outsourceId)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<OutsourceRecoveryDto>>>($"{BaseUrl}/outsource/{outsourceId}/recoveries")
                   ?? ApiResponse<List<OutsourceRecoveryDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<OutsourceRecoveryDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<OutsourceRecoveryDto>> CreateOutsourceRecoveryAsync(CreateOutsourceRecoveryRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateOutsourceRecoveryRequest, ApiResponse<OutsourceRecoveryDto>>($"{BaseUrl}/recovery", request)
                   ?? ApiResponse<OutsourceRecoveryDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<OutsourceRecoveryDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> DeleteOutsourceRecoveryAsync(int id)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/recovery/{id}")
                   ?? ApiResponse<object>.Fail("删除失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 检验到料 ==========

    public async Task<ApiResponse<MaterialReceiveCheckDto>> GetMaterialReceiveCheckAsync(int batchId)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<MaterialReceiveCheckDto>>($"{BaseUrl}/{batchId}/material-check")
                   ?? ApiResponse<MaterialReceiveCheckDto>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<MaterialReceiveCheckDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<MaterialReceiveCheckDto>> CreateMaterialReceiveCheckAsync(CreateMaterialReceiveCheckRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateMaterialReceiveCheckRequest, ApiResponse<MaterialReceiveCheckDto>>($"{BaseUrl}/material-check", request)
                   ?? ApiResponse<MaterialReceiveCheckDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<MaterialReceiveCheckDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<MaterialReceiveCheckDto>> UpdateMaterialReceiveCheckAsync(int id, UpdateMaterialReceiveCheckRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateMaterialReceiveCheckRequest, ApiResponse<MaterialReceiveCheckDto>>($"{BaseUrl}/material-check/{id}", request)
                   ?? ApiResponse<MaterialReceiveCheckDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<MaterialReceiveCheckDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> DeleteMaterialReceiveCheckAsync(int id)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/material-check/{id}")
                   ?? ApiResponse<object>.Fail("删除失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 跨批次查询（用于独立页面） ==========

    public async Task<ApiResponse<PagedResult<ProductionRecordDto>>> GetAllProductionRecordsAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null, bool isDescending = true, DateTime? execDateFrom = null, DateTime? execDateTo = null)
    {
        try
        {
            var url = $"{BaseUrl}/all/records?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (execDateFrom.HasValue) url += $"&execDateFrom={execDateFrom.Value:yyyy-MM-dd}";
            if (execDateTo.HasValue) url += $"&execDateTo={execDateTo.Value:yyyy-MM-dd}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<ProductionRecordDto>>>(url)
                   ?? ApiResponse<PagedResult<ProductionRecordDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<ProductionRecordDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PagedResult<SectionOutsourceDto>>> GetAllSectionOutsourcesAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null, bool isDescending = true)
    {
        try
        {
            var url = $"{BaseUrl}/all/outsources?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<SectionOutsourceDto>>>(url)
                   ?? ApiResponse<PagedResult<SectionOutsourceDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<SectionOutsourceDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PagedResult<OutsourceRecoveryDto>>> GetAllOutsourceRecoveriesAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null, bool isDescending = true)
    {
        try
        {
            var url = $"{BaseUrl}/all/recoveries?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<OutsourceRecoveryDto>>>(url)
                   ?? ApiResponse<PagedResult<OutsourceRecoveryDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<OutsourceRecoveryDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PagedResult<MaterialReceiveCheckDto>>> GetAllMaterialReceiveChecksAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, string? sortBy = null, bool isDescending = true, DateTime? receiveDateFrom = null, DateTime? receiveDateTo = null)
    {
        try
        {
            var url = $"{BaseUrl}/all/material-checks?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (receiveDateFrom.HasValue) url += $"&receiveDateFrom={receiveDateFrom.Value:yyyy-MM-dd}";
            if (receiveDateTo.HasValue) url += $"&receiveDateTo={receiveDateTo.Value:yyyy-MM-dd}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<MaterialReceiveCheckDto>>>(url)
                   ?? ApiResponse<PagedResult<MaterialReceiveCheckDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<MaterialReceiveCheckDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<SectionOutsourceDto>>> BatchCreateSectionOutsourcesAsync(List<CreateSectionOutsourceRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateSectionOutsourceRequest>, ApiResponse<List<SectionOutsourceDto>>>($"{BaseUrl}/outsources/batch", requests)
                   ?? ApiResponse<List<SectionOutsourceDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<SectionOutsourceDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<OutsourceRecoveryDto>>> BatchCreateOutsourceRecoveriesAsync(List<CreateOutsourceRecoveryRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateOutsourceRecoveryRequest>, ApiResponse<List<OutsourceRecoveryDto>>>($"{BaseUrl}/recoveries/batch", requests)
                   ?? ApiResponse<List<OutsourceRecoveryDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<OutsourceRecoveryDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<MaterialReceiveCheckDto>>> BatchCreateMaterialReceiveChecksAsync(List<CreateMaterialReceiveCheckRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateMaterialReceiveCheckRequest>, ApiResponse<List<MaterialReceiveCheckDto>>>($"{BaseUrl}/material-checks/batch", requests)
                   ?? ApiResponse<List<MaterialReceiveCheckDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<MaterialReceiveCheckDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 批次跟踪字段 ==========

    public async Task<ApiResponse<object>> RefreshBatchTrackingAsync(int batchId)
    {
        try
        {
            return await _http.PostAsJsonAsync<object, ApiResponse<object>>($"{BaseUrl}/{batchId}/refresh-tracking", new { })
                   ?? ApiResponse<object>.Fail("刷新失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 打印 ==========

    public async Task<ApiResponse<string>> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        try
        {
            var request = new ProductionRecordPrintBatchRequest { Ids = ids, Columns = columns };
            var response = await _http.PostAsJsonAsync<ProductionRecordPrintBatchRequest, ApiResponse<string>>(
                $"{BaseUrl}/print-batch", request);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> PrintAllAsync(string? keyword = null, string? sortBy = null, bool isDescending = true, List<PrintColumnDef>? columns = null, DateTime? execDateFrom = null, DateTime? execDateTo = null)
    {
        try
        {
            var request = new ProductionRecordPrintAllRequest
            {
                Keyword = keyword,
                SortBy = sortBy,
                IsDescending = isDescending,
                Columns = columns ?? new(),
                ExecDateFrom = execDateFrom,
                ExecDateTo = execDateTo
            };
            var response = await _http.PostAsJsonAsync<ProductionRecordPrintAllRequest, ApiResponse<string>>(
                $"{BaseUrl}/print-all", request);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> PrintMaterialCheckBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        try
        {
            var request = new MaterialCheckPrintBatchRequest { Ids = ids, Columns = columns };
            var response = await _http.PostAsJsonAsync<MaterialCheckPrintBatchRequest, ApiResponse<string>>(
                $"{BaseUrl}/material-check/print-batch", request);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> PrintMaterialCheckAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? receiveDateFrom = null, DateTime? receiveDateTo = null)
    {
        try
        {
            var request = new MaterialCheckPrintAllRequest
            {
                Keyword = keyword,
                SortBy = sortBy,
                IsDescending = isDescending,
                Columns = columns,
                ReceiveDateFrom = receiveDateFrom,
                ReceiveDateTo = receiveDateTo
            };
            var response = await _http.PostAsJsonAsync<MaterialCheckPrintAllRequest, ApiResponse<string>>(
                $"{BaseUrl}/material-check/print-all", request);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }
}
