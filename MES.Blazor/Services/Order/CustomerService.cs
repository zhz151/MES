// 文件路径: MES.Blazor/Services/CustomerService.cs
using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Order;

namespace MES.Blazor.Services;

public class CustomerService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.Customer;

    public CustomerService(AuthHttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// 分页查询客户列表（支持关键字搜索）
    /// </summary>
    public async Task<ApiResponse<PagedResult<CustomerProfileDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            // 使用小写的 true/false
            var isDescending = query.IsDescending ? "true" : "false";
            var encodedSortBy = Uri.EscapeDataString(query.SortBy ?? ApiEndpoints.DefaultSortBy);

            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={encodedSortBy}&isDescending={isDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
            {
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            }
            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<CustomerProfileDto>>>(url);
            return response ?? ApiResponse<PagedResult<CustomerProfileDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<CustomerProfileDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 分页查询客户列表（直接参数风格，支持筛选过滤）
    /// </summary>
    public async Task<ApiResponse<PagedResult<CustomerProfileDto>>> GetAllAsync(
        int pageIndex = 1, int pageSize = 20,
        string? keyword = null, string? sortBy = null,
        bool isDescending = true, List<FilterDescriptor>? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={pageIndex}&pageSize={pageSize}&sortBy={Uri.EscapeDataString(sortBy ?? ApiEndpoints.DefaultSortBy)}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(filters))}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<CustomerProfileDto>>>(url);
            return response ?? ApiResponse<PagedResult<CustomerProfileDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<CustomerProfileDto>>.Fail($"网络错误: {ex.Message}");
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
    /// 获取所有客户（用于下拉框）
    /// </summary>
    public async Task<List<CustomerProfileDto>> GetAllAsync()
    {
        var result = await GetPagedAsync(new QueryParams { PageSize = 999, Keyword = null });
        if (result.Success && result.Data != null)
        {
            return result.Data.Items;
        }
        return new List<CustomerProfileDto>();
    }

    /// <summary>
    /// 根据ID获取客户详情
    /// </summary>
    public async Task<ApiResponse<CustomerProfileDto>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<CustomerProfileDto>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<CustomerProfileDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<CustomerProfileDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 创建客户
    /// </summary>
    public async Task<ApiResponse<CustomerProfileDto>> CreateAsync(CreateCustomerRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<CreateCustomerRequest, ApiResponse<CustomerProfileDto>>(BaseUrl, request);
            return response ?? ApiResponse<CustomerProfileDto>.Fail("创建失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<CustomerProfileDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新客户
    /// </summary>
    public async Task<ApiResponse<CustomerProfileDto>> UpdateAsync(int id, UpdateCustomerRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<UpdateCustomerRequest, ApiResponse<CustomerProfileDto>>($"{BaseUrl}/{id}", request);
            return response ?? ApiResponse<CustomerProfileDto>.Fail("更新失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<CustomerProfileDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除客户（物理删除）
    /// </summary>
    public async Task<ApiResponse<object>> DeleteAsync(int id)
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
}