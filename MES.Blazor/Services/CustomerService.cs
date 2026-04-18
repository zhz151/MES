// 文件路径: MES.Blazor/Services/CustomerService.cs
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class CustomerService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/customer";

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
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
            {
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            }
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<CustomerProfileDto>>>(url);
            return response ?? ApiResponse<PagedResult<CustomerProfileDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<CustomerProfileDto>>.Fail($"网络错误: {ex.Message}");
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
    /// 删除客户（软删除）
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