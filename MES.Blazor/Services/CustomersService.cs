// 文件路径: MES.Blazor/Services/CustomerService.cs
using System.Net.Http.Json;
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// Customer frontend service implementation
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "api/customer";

    public CustomerService(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Get paged customer list
    /// </summary>
    public async Task<ApiResponse<PagedResult<CustomerProfileDto>>> GetPagedAsync(QueryParams query)
    {
        var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<CustomerProfileDto>>>(
            $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&keyword={query.Keyword}&sortBy={query.SortBy}&isDescending={query.IsDescending}");
        return response ?? new ApiResponse<PagedResult<CustomerProfileDto>> { Success = false, Message = "Failed to get data" };
    }

    /// <summary>
    /// Get all customers (for dropdown)
    /// </summary>
    public async Task<List<CustomerProfileDto>> GetAllAsync()
    {
        var result = await GetPagedAsync(new QueryParams { PageSize = 999 });
        if (result.Success && result.Data != null)
        {
            return result.Data.Items;
        }
        return new List<CustomerProfileDto>();
    }

    /// <summary>
    /// Get customer details by ID
    /// </summary>
    public async Task<ApiResponse<CustomerProfileDto>> GetByIdAsync(int id)
    {
        var response = await _http.GetFromJsonAsync<ApiResponse<CustomerProfileDto>>($"{BaseUrl}/{id}");
        return response ?? new ApiResponse<CustomerProfileDto> { Success = false, Message = "Failed to get customer details" };
    }

    /// <summary>
    /// Create customer
    /// </summary>
    public async Task<ApiResponse<CustomerProfileDto>> CreateAsync(CreateCustomerRequest request)
    {
        var response = await _http.PostAsJsonAsync(BaseUrl, request);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CustomerProfileDto>>();
        return result ?? new ApiResponse<CustomerProfileDto> { Success = false, Message = "Failed to create customer" };
    }

    /// <summary>
    /// Update customer
    /// </summary>
    public async Task<ApiResponse<CustomerProfileDto>> UpdateAsync(int id, UpdateCustomerRequest request)
    {
        var response = await _http.PutAsJsonAsync($"{BaseUrl}/{id}", request);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CustomerProfileDto>>();
        return result ?? new ApiResponse<CustomerProfileDto> { Success = false, Message = "Failed to update customer" };
    }

    /// <summary>
    /// Delete customer
    /// </summary>
    public async Task<ApiResponse<object>> DeleteAsync(int id)
    {
        var response = await _http.DeleteAsync($"{BaseUrl}/{id}");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        return result ?? new ApiResponse<object> { Success = false, Message = "Failed to delete customer" };
    }
}