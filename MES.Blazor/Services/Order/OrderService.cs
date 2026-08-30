using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.Enums;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.Infrastructure;

namespace MES.Blazor.Services;

public class OrderService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.Order;

    public OrderService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<SalesOrderListDto>>> GetPagedAsync(
        QueryParams query,
        bool? hasTechnicalRequirement = null,
        List<SalesOrderStatus>? statuses = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        DateTime? deliveryDateFrom = null,
        DateTime? deliveryDateTo = null,
        OrderDeliveryEstimateFilterDto? estimateFilter = null)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";

            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";

            if (hasTechnicalRequirement.HasValue)
                url += $"&technicalStatus={(hasTechnicalRequirement.Value ? "Edited" : "NotEdited")}";

            if (statuses != null && statuses.Any())
            {
                var statusParam = string.Join(",", statuses.Select(s => s.ToString()));
                url += $"&orderStatus={Uri.EscapeDataString(statusParam)}";
            }

            if (dateFrom.HasValue) url += $"&signDateFrom={dateFrom.Value:yyyy-MM-dd}";
            if (dateTo.HasValue) url += $"&signDateTo={dateTo.Value:yyyy-MM-dd}";

            if (deliveryDateFrom.HasValue) url += $"&deliveryDateFrom={deliveryDateFrom.Value:yyyy-MM-dd}";
            if (deliveryDateTo.HasValue) url += $"&deliveryDateTo={deliveryDateTo.Value:yyyy-MM-dd}";

            if (estimateFilter != null)
                url += $"&estimateFilter={Uri.EscapeDataString(JsonSerializer.Serialize(estimateFilter))}";

            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<SalesOrderListDto>>>(url);
            return response ?? ApiResponse<PagedResult<SalesOrderListDto>>.Fail("获取订单列表失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<SalesOrderListDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<SalesOrderDetailDto>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<SalesOrderDetailDto>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<SalesOrderDetailDto>.Fail("获取订单详情失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<SalesOrderDetailDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据订单号获取订单ID
    /// </summary>
    public async Task<ApiResponse<int?>> GetIdByOrderNumberAsync(string orderNo)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<int?>>($"{BaseUrl}/by-number/{Uri.EscapeDataString(orderNo)}");
            return response ?? ApiResponse<int?>.Fail("获取订单ID失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int?>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<SalesOrderListDto>> CreateAsync(CreateSalesOrderRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<CreateSalesOrderRequest, ApiResponse<SalesOrderListDto>>(BaseUrl, request);
            return response ?? ApiResponse<SalesOrderListDto>.Fail("创建订单失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<SalesOrderListDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<SalesOrderListDto>> UpdateAsync(int id, UpdateSalesOrderRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<UpdateSalesOrderRequest, ApiResponse<SalesOrderListDto>>($"{BaseUrl}/{id}", request);
            return response ?? ApiResponse<SalesOrderListDto>.Fail("更新订单失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<SalesOrderListDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<object>> DeleteAsync(int id)
    {
        try
        {
            var response = await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<object>.Fail("删除订单失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<SaveAllOrderResponse>> SaveAllAsync(int id, SaveAllOrderRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<SaveAllOrderRequest, ApiResponse<SaveAllOrderResponse>>(
                $"{BaseUrl}/{id}/save-all", request);
            return response ?? ApiResponse<SaveAllOrderResponse>.Fail("批量保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<SaveAllOrderResponse>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
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

    /// <summary>
    /// 获取订单接单·出库及现负荷汇总（本年按月：接单量/出库量/库存完工/库存未完工）
    /// </summary>
    public async Task<ApiResponse<OrderInOutSummaryDto>> GetInOutSummaryAsync(int year)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<OrderInOutSummaryDto>>($"{BaseUrl}/in-out-summary?year={year}");
            return response ?? ApiResponse<OrderInOutSummaryDto>.Fail("获取订单接单·出库及现负荷汇总失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderInOutSummaryDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>获取订单交期预估（业务总况两小表：订单完成预估 / 延期交货订单预估）</summary>
    public async Task<ApiResponse<OrderDeliveryEstimateDto>> GetDeliveryEstimateAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<OrderDeliveryEstimateDto>>($"{BaseUrl}/delivery-estimate");
            return response ?? ApiResponse<OrderDeliveryEstimateDto>.Fail("获取订单交期预估失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderDeliveryEstimateDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<OperationLogDto>>> GetOperationLogsAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<OperationLogDto>>>($"{BaseUrl}/{id}/operation-logs");
            return response ?? ApiResponse<List<OperationLogDto>>.Fail("获取操作日志失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<OperationLogDto>>.Fail($"网络错误: {ex.Message}");
        }
    }
}