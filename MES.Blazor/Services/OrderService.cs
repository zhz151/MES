// 文件路径: MES.Blazor/Services/OrderService.cs
using System.Net.Http.Json;
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 订单前端服务实现
/// </summary>
public class OrderService : IOrderService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "api/order";

    public OrderService(HttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<SalesOrderListDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<SalesOrderListDto>>>(
                $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&keyword={Uri.EscapeDataString(query.Keyword ?? "")}&sortBy={query.SortBy}&isDescending={query.IsDescending}");
            return response ?? ApiResponse<PagedResult<SalesOrderListDto>>.Fail("获取数据失败");
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
            var response = await _http.PostAsJsonAsync(BaseUrl, request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<SalesOrderListDto>>();
            return result ?? ApiResponse<SalesOrderListDto>.Fail("创建订单失败");
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
            var response = await _http.PutAsJsonAsync($"{BaseUrl}/{id}", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<SalesOrderListDto>>();
            return result ?? ApiResponse<SalesOrderListDto>.Fail("更新订单失败");
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
            var response = await _http.DeleteAsync($"{BaseUrl}/{id}");
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            return result ?? ApiResponse<object>.Fail("删除订单失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail($"网络错误: {ex.Message}");
        }
    }
}