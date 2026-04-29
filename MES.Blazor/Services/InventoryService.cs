using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class InventoryService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/inventory";

    public InventoryService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<InventoryBatchDto>>> GetPagedAsync(InventoryQueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";

            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";

            if (query.WarehouseId.HasValue)
                url += $"&warehouseId={query.WarehouseId.Value}";

            if (!string.IsNullOrEmpty(query.MaterialType))
                url += $"&materialType={Uri.EscapeDataString(query.MaterialType)}";

            if (!string.IsNullOrEmpty(query.PlantGrade))
                url += $"&plantGrade={Uri.EscapeDataString(query.PlantGrade)}";

            url += $"&onlyWithStock={query.OnlyWithStock}";

            if (!string.IsNullOrEmpty(query.WorkOrderNo))
                url += $"&workOrderNo={Uri.EscapeDataString(query.WorkOrderNo)}";

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

    public async Task<ApiResponse<PagedResult<OutboundRecordDto>>> GetOutboundRecordsAsync(OutboundQueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/outbound-records?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";

            if (query.InventoryBatchId.HasValue)
                url += $"&inventoryBatchId={query.InventoryBatchId.Value}";

            if (!string.IsNullOrEmpty(query.OutboundType))
                url += $"&outboundType={Uri.EscapeDataString(query.OutboundType)}";

            if (query.StartDate.HasValue)
                url += $"&startDate={query.StartDate.Value:yyyy-MM-dd}";

            if (query.EndDate.HasValue)
                url += $"&endDate={query.EndDate.Value:yyyy-MM-dd}";

            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";

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
}
