using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.Warehouse;

namespace MES.Blazor.Services;

public class InventoryService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.Inventory;

    public InventoryService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<List<InventoryBatchDto>>> GetAllListAsync(InventoryQueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/all?sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";

            if (query.WarehouseId.HasValue)
                url += $"&warehouseId={query.WarehouseId.Value}";

            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";

            if (!string.IsNullOrEmpty(query.MaterialType))
                url += $"&materialType={Uri.EscapeDataString(query.MaterialType)}";

            if (!string.IsNullOrEmpty(query.PlantGrade))
                url += $"&plantGrade={Uri.EscapeDataString(query.PlantGrade)}";

            url += $"&onlyWithStock={query.OnlyWithStock}";

            var response = await _http.GetFromJsonAsync<ApiResponse<List<InventoryBatchDto>>>(url);
            return response ?? ApiResponse<List<InventoryBatchDto>>.Fail("获取库存列表失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<InventoryBatchDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PagedResult<InventoryBatchDto>>> GetPagedAsync(InventoryQueryParams query, string? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";

            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";

            if (query.WarehouseId.HasValue)
                url += $"&warehouseId={query.WarehouseId.Value}";

            if (query.InboundDateFrom.HasValue)
                url += $"&inboundDateFrom={query.InboundDateFrom.Value:yyyy-MM-dd}";

            if (query.InboundDateTo.HasValue)
                url += $"&inboundDateTo={query.InboundDateTo.Value:yyyy-MM-dd}";

            if (!string.IsNullOrEmpty(query.MaterialType))
                url += $"&materialType={Uri.EscapeDataString(query.MaterialType)}";

            if (!string.IsNullOrEmpty(query.PlantGrade))
                url += $"&plantGrade={Uri.EscapeDataString(query.PlantGrade)}";

            url += $"&onlyWithStock={query.OnlyWithStock}";

            if (!string.IsNullOrEmpty(query.WorkOrderNo))
                url += $"&workOrderNo={Uri.EscapeDataString(query.WorkOrderNo)}";

            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<InventoryBatchDto>>>(url);
            return response ?? ApiResponse<PagedResult<InventoryBatchDto>>.Fail("获取库存列表失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<InventoryBatchDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<InventoryBatchDto>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<InventoryBatchDto>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<InventoryBatchDto>.Fail("获取批次详情失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<InventoryBatchDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<BatchInboundResult>> BatchInboundAsync(BatchInboundRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<BatchInboundRequest, ApiResponse<BatchInboundResult>>($"{BaseUrl}/batch-inbound", request);
            return response ?? ApiResponse<BatchInboundResult>.Fail("批量入库失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<BatchInboundResult>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<InventoryBatchDto>> InboundAsync(CreateInboundRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<CreateInboundRequest, ApiResponse<InventoryBatchDto>>($"{BaseUrl}/inbound", request);
            return response ?? ApiResponse<InventoryBatchDto>.Fail("入库失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<InventoryBatchDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<OutboundRecordDto>> OutboundAsync(CreateOutboundRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<CreateOutboundRequest, ApiResponse<OutboundRecordDto>>($"{BaseUrl}/outbound", request);
            return response ?? ApiResponse<OutboundRecordDto>.Fail("出库失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<OutboundRecordDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<BatchOutboundResult>> BatchOutboundAsync(BatchOutboundRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<BatchOutboundRequest, ApiResponse<BatchOutboundResult>>($"{BaseUrl}/batch-outbound", request);
            return response ?? ApiResponse<BatchOutboundResult>.Fail("批量出库失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<BatchOutboundResult>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PagedResult<OutboundRecordDto>>> GetOutboundRecordsAsync(OutboundQueryParams query, string? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/outbound-records?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";

            if (query.InventoryBatchId.HasValue)
                url += $"&inventoryBatchId={query.InventoryBatchId.Value}";

            if (!string.IsNullOrEmpty(query.OutboundType))
                url += $"&outboundType={Uri.EscapeDataString(query.OutboundType)}";

            if (query.WarehouseId.HasValue)
                url += $"&warehouseId={query.WarehouseId.Value}";

            if (query.StartDate.HasValue)
                url += $"&startDate={query.StartDate.Value:yyyy-MM-dd}";

            if (query.EndDate.HasValue)
                url += $"&endDate={query.EndDate.Value:yyyy-MM-dd}";

            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";

            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<OutboundRecordDto>>>(url);
            return response ?? ApiResponse<PagedResult<OutboundRecordDto>>.Fail("获取出库记录失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<OutboundRecordDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<InventoryBatchDto>> UpdateInventoryBatchAsync(int id, UpdateInventoryBatchRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<UpdateInventoryBatchRequest, ApiResponse<InventoryBatchDto>>($"{BaseUrl}/{id}", request);
            return response ?? ApiResponse<InventoryBatchDto>.Fail("更新失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<InventoryBatchDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<object>> HardDeleteInventoryBatchAsync(int id)
    {
        try
        {
            var response = await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<object>.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<OutboundRecordDto>> UpdateOutboundRecordAsync(long id, UpdateOutboundRecordRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<UpdateOutboundRecordRequest, ApiResponse<OutboundRecordDto>>($"{BaseUrl}/outbound-records/{id}", request);
            return response ?? ApiResponse<OutboundRecordDto>.Fail("更新失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<OutboundRecordDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<object>> HardDeleteOutboundRecordAsync(long id)
    {
        try
        {
            var response = await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/outbound-records/{id}");
            return response ?? ApiResponse<object>.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<SourceOrderValidationResult>> ValidateSourceOrderAsync(string sourceOrderNo, string inboundSource, int? sourceOrderSequence = null)
    {
        try
        {
            return await _http.PostAsJsonAsync<object, ApiResponse<SourceOrderValidationResult>>(
                $"{BaseUrl}/validate-source-order",
                new { sourceOrderNo, inboundSource, sourceOrderSequence })
                ?? ApiResponse<SourceOrderValidationResult>.Fail("验证失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<SourceOrderValidationResult>.Fail($"网络错误: {ex.Message}");
        }
    }

    // ========== 工单号验证 ==========

    public async Task<ApiResponse<List<string>>> ValidateWorkOrderNosAsync(int warehouseId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<string>>>($"{BaseUrl}/validate-workorder-nos/{warehouseId}");
            return response ?? ApiResponse<List<string>>.Fail("验证失败");
        }
        catch (Exception ex) { return ApiResponse<List<string>>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>
    /// 获取入库批次中工单号不存在的批次列表（实时扫描）
    /// </summary>
    public async Task<ApiResponse<List<BatchWorkOrderMismatchDto>>> GetMismatchedBatchesAsync(int? warehouseId = null)
    {
        try
        {
            var url = $"{BaseUrl}/mismatched-batches";
            if (warehouseId.HasValue)
                url += $"?warehouseId={warehouseId.Value}";
            var response = await _http.GetFromJsonAsync<ApiResponse<List<BatchWorkOrderMismatchDto>>>(url);
            return response ?? ApiResponse<List<BatchWorkOrderMismatchDto>>.Fail("查询失败");
        }
        catch (Exception ex) { return ApiResponse<List<BatchWorkOrderMismatchDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 打印 ==========

    [Obsolete("请直接使用 openPdfFromApi 调用 -file 端点")]
    public async Task<ApiResponse<string>> PrintInventoryAllAsync(InventoryPrintAllRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<InventoryPrintAllRequest, ApiResponse<string>>($"{BaseUrl}/print-inventory-all", request);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    [Obsolete("请直接使用 openPdfFromApi 调用 -file 端点")]
    public async Task<ApiResponse<string>> PrintInventorySelectedAsync(InventoryPrintSelectedRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<InventoryPrintSelectedRequest, ApiResponse<string>>($"{BaseUrl}/print-inventory-selected", request);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    [Obsolete("请直接使用 openPdfFromApi 调用 -file 端点")]
    public async Task<ApiResponse<string>> PrintOutboundAllAsync(OutboundPrintAllRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<OutboundPrintAllRequest, ApiResponse<string>>($"{BaseUrl}/print-outbound-all", request);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    [Obsolete("请直接使用 openPdfFromApi 调用 -file 端点")]
    public async Task<ApiResponse<string>> PrintOutboundSelectedAsync(OutboundPrintSelectedRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<OutboundPrintSelectedRequest, ApiResponse<string>>($"{BaseUrl}/print-outbound-selected", request);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 筛选上下文 ==========

    /// <summary>
    /// 获取出库记录筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/outbound-filter-contexts");
            return response ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取库存批次筛选上下文（各列去重值），用于 ExcelFilter 下拉选项（入库/库存页面使用）
    /// </summary>
    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetInventoryFilterContextsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/inventory-filter-contexts");
            return response ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}");
        }
    }
}
