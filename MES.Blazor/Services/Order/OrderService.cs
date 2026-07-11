using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.Enums;
using MES.Core.DTOs.Order;

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
        DateTime? deliveryDateTo = null)
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

    public async Task<ApiResponse<OrderItemDto>> AddItemAsync(int orderId, AddOrderItemRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<AddOrderItemRequest, ApiResponse<OrderItemDto>>($"{BaseUrl}/{orderId}/items", request);
            return response ?? ApiResponse<OrderItemDto>.Fail("添加项次失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderItemDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<OrderItemDto>> UpdateItemAsync(int orderId, int itemId, UpdateOrderItemRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<UpdateOrderItemRequest, ApiResponse<OrderItemDto>>($"{BaseUrl}/{orderId}/items/{itemId}", request);
            return response ?? ApiResponse<OrderItemDto>.Fail("更新项次失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderItemDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<object>> DeleteItemAsync(int orderId, int itemId)
    {
        try
        {
            var response = await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/{orderId}/items/{itemId}");
            return response ?? ApiResponse<object>.Fail("删除项次失败");
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
    /// 获取所有订单列表数据（无分页，供客户端筛选排序）
    /// </summary>
    public async Task<ApiResponse<List<SalesOrderListDto>>> GetAllAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<SalesOrderListDto>>>($"{BaseUrl}/list-all");
            return response ?? ApiResponse<List<SalesOrderListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<SalesOrderListDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 手动刷新订单列表读模型（即时更新）
    /// </summary>
    public async Task<ApiResponse> RefreshAsync()
    {
        try
        {
            var response = await _http.PostAsJsonAsync<object, ApiResponse>($"{BaseUrl}/refresh", new object());
            return response ?? ApiResponse.Fail("刷新失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }

    // ========== 打印 ==========

    /// <summary>
    /// 打印单个订单
    /// </summary>
    [Obsolete("请直接使用 openPdfFromApi 调用 -file 端点")]
    public async Task<ApiResponse<string>> PrintOrderAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<string>>($"{BaseUrl}/{id}/print");
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 批量打印选定订单
    /// </summary>
    [Obsolete("请直接使用 openPdfFromApi 调用 -file 端点")]
    public async Task<ApiResponse<string>> PrintOrderBatchAsync(int[] ids)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<OrderPrintBatchRequest, ApiResponse<string>>($"{BaseUrl}/print-batch", new OrderPrintBatchRequest { Ids = ids });
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 打印订单技术要求
    /// </summary>
    [Obsolete("请直接使用 openPdfFromApi 调用 -file 端点")]
    public async Task<ApiResponse<string>> PrintOrderRequirementsAsync(int orderId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<string>>($"{BaseUrl}/{orderId}/requirements/print");
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail($"网络错误: {ex.Message}");
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
}