// 文件路径: MES.Blazor/Services/OrderService.cs
using System.Net.Http.Json;
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 订单服务实现（前端）
/// </summary>
public class OrderService : IOrderService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "api/order";

    public OrderService(HttpClient http)
    {
        _http = http;
    }

    // ========== 订单管理实现 ==========

    /// <summary>
    /// 分页查询订单列表
    /// </summary>
    public async Task<ApiResponse<PagedResult<SalesOrderListDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
            {
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            }

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<SalesOrderListDto>>>(url);
            return response ?? ApiResponse<PagedResult<SalesOrderListDto>>.Fail("获取订单列表失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<SalesOrderListDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据ID获取订单详情
    /// </summary>
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
    /// 创建订单
    /// </summary>
    public async Task<ApiResponse<SalesOrderListDto>> CreateAsync(CreateSalesOrderRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(BaseUrl, request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<SalesOrderListDto>>();

            if (response.IsSuccessStatusCode)
            {
                // 修复：使用 null 合并运算符，如果 result 为 null 则返回失败响应
                return result ?? ApiResponse<SalesOrderListDto>.Fail("创建订单失败");
            }

            // 处理业务错误（如400状态码）
            if (result != null && !result.Success)
            {
                return result;
            }

            return ApiResponse<SalesOrderListDto>.Fail($"创建订单失败: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return ApiResponse<SalesOrderListDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新订单
    /// </summary>
    public async Task<ApiResponse<SalesOrderListDto>> UpdateAsync(int id, UpdateSalesOrderRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"{BaseUrl}/{id}", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<SalesOrderListDto>>();

            if (response.IsSuccessStatusCode)
            {
                // 修复：使用 null 合并运算符
                return result ?? ApiResponse<SalesOrderListDto>.Fail("更新订单失败");
            }

            if (result != null && !result.Success)
            {
                return result;
            }

            return ApiResponse<SalesOrderListDto>.Fail($"更新订单失败: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return ApiResponse<SalesOrderListDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除订单（软删除）
    /// </summary>
    public async Task<ApiResponse> DeleteAsync(int id)  // 注意：返回 ApiResponse（无泛型）
    {
        try
        {
            var response = await _http.DeleteAsync($"{BaseUrl}/{id}");
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>();

            if (response.IsSuccessStatusCode)
            {
                // 修复：使用 null 合并运算符
                return result ?? ApiResponse.Ok("删除成功");
            }

            if (result != null && !result.Success)
            {
                return result;
            }

            return ApiResponse.Fail($"删除订单失败: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }

    // ========== 项次管理实现 ==========

    /// <summary>
    /// 添加订单项次
    /// </summary>
    public async Task<ApiResponse<OrderItemDto>> AddItemAsync(int orderId, AddOrderItemRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"{BaseUrl}/{orderId}/items", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrderItemDto>>();

            if (response.IsSuccessStatusCode)
            {
                // 修复：使用 null 合并运算符
                return result ?? ApiResponse<OrderItemDto>.Fail("添加项次失败");
            }

            if (result != null && !result.Success)
            {
                return result;
            }

            return ApiResponse<OrderItemDto>.Fail($"添加项次失败: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderItemDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新订单项次
    /// </summary>
    public async Task<ApiResponse<OrderItemDto>> UpdateItemAsync(int orderId, int itemId, UpdateOrderItemRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"{BaseUrl}/{orderId}/items/{itemId}", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrderItemDto>>();

            if (response.IsSuccessStatusCode)
            {
                // 修复：使用 null 合并运算符
                return result ?? ApiResponse<OrderItemDto>.Fail("更新项次失败");
            }

            if (result != null && !result.Success)
            {
                return result;
            }

            return ApiResponse<OrderItemDto>.Fail($"更新项次失败: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderItemDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除订单项次（软删除）
    /// </summary>
    public async Task<ApiResponse> DeleteItemAsync(int orderId, int itemId)  // 注意：返回 ApiResponse（无泛型）
    {
        try
        {
            var response = await _http.DeleteAsync($"{BaseUrl}/{orderId}/items/{itemId}");
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>();

            if (response.IsSuccessStatusCode)
            {
                // 修复：使用 null 合并运算符
                return result ?? ApiResponse.Ok("删除项次成功");
            }

            if (result != null && !result.Success)
            {
                return result;
            }

            return ApiResponse.Fail($"删除项次失败: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }
}