using System.Net.Http.Json;
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

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
            return response ?? ApiResponse<PagedResult<SalesOrderListDto>>.Fail("Failed to get data");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<SalesOrderListDto>>.Fail($"Network error: {ex.Message}");
        }
    }

    public async Task<ApiResponse<SalesOrderDetailDto>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<SalesOrderDetailDto>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<SalesOrderDetailDto>.Fail("Failed to get order details");
        }
        catch (Exception ex)
        {
            return ApiResponse<SalesOrderDetailDto>.Fail($"Network error: {ex.Message}");
        }
    }

    public async Task<ApiResponse<SalesOrderListDto>> CreateAsync(CreateSalesOrderRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(BaseUrl, request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<SalesOrderListDto>>();
            return result ?? ApiResponse<SalesOrderListDto>.Fail("Failed to create order");
        }
        catch (Exception ex)
        {
            return ApiResponse<SalesOrderListDto>.Fail($"Network error: {ex.Message}");
        }
    }

    public async Task<ApiResponse<SalesOrderListDto>> UpdateAsync(int id, UpdateSalesOrderRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"{BaseUrl}/{id}", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<SalesOrderListDto>>();
            return result ?? ApiResponse<SalesOrderListDto>.Fail("Failed to update order");
        }
        catch (Exception ex)
        {
            return ApiResponse<SalesOrderListDto>.Fail($"Network error: {ex.Message}");
        }
    }

    public async Task<ApiResponse<object>> DeleteAsync(int id)
    {
        try
        {
            var response = await _http.DeleteAsync($"{BaseUrl}/{id}");
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            return result ?? ApiResponse<object>.Fail("Failed to delete order");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail($"Network error: {ex.Message}");
        }
    }
}