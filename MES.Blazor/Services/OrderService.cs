using MES.Core.DTOs;
using MES.Core.Models;
using MES.Core.Enums;

namespace MES.Blazor.Services;

public class OrderService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/order";

    public OrderService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<SalesOrderListDto>>> GetPagedAsync(
        QueryParams query, 
        bool? hasTechnicalRequirement = null, 
        List<SalesOrderStatus>? statuses = null)
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

    // ========== 打印 ==========

    /// <summary>
    /// 打印单个订单
    /// </summary>
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
    /// 按筛选条件打印全部订单
    /// </summary>
    public async Task<ApiResponse<string>> PrintOrderAllAsync(string? keyword = null, string? technicalStatus = null, string? orderStatus = null, string? sortBy = null, bool isDescending = false)
    {
        try
        {
            var request = new OrderPrintAllRequest
            {
                Keyword = keyword,
                TechnicalStatus = technicalStatus,
                OrderStatus = orderStatus,
                SortBy = sortBy,
                IsDescending = isDescending
            };
            var response = await _http.PostAsJsonAsync<OrderPrintAllRequest, ApiResponse<string>>($"{BaseUrl}/print-all", request);
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
}