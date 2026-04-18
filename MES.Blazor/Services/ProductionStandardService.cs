// 文件路径: MES.Blazor/Services/ProductionStandardService.cs
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class ProductionStandardService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/standard";

    public ProductionStandardService(AuthHttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// 分页查询产品标准列表（支持关键字搜索）
    /// </summary>
    public async Task<ApiResponse<PagedResult<ProductionStandardDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
            {
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            }
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<ProductionStandardDto>>>(url);
            return response ?? ApiResponse<PagedResult<ProductionStandardDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<ProductionStandardDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取所有产品标准（用于下拉框）
    /// </summary>
    public async Task<List<ProductionStandardDto>> GetAllAsync(bool onlyActive = true)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<ProductionStandardDto>>>($"{BaseUrl}/all?onlyActive={onlyActive}");
            if (response != null && response.Success && response.Data != null)
            {
                return response.Data;
            }
            return new List<ProductionStandardDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAllAsync error: {ex.Message}");
            return new List<ProductionStandardDto>();
        }
    }

    /// <summary>
    /// 根据ID获取产品标准详情
    /// </summary>
    public async Task<ApiResponse<ProductionStandardDto>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<ProductionStandardDto>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<ProductionStandardDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<ProductionStandardDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 创建产品标准
    /// </summary>
    public async Task<ApiResponse<ProductionStandardDto>> CreateAsync(CreateProductionStandardRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<CreateProductionStandardRequest, ApiResponse<ProductionStandardDto>>(BaseUrl, request);
            return response ?? ApiResponse<ProductionStandardDto>.Fail("创建失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<ProductionStandardDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新产品标准
    /// </summary>
    public async Task<ApiResponse<ProductionStandardDto>> UpdateAsync(int id, UpdateProductionStandardRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<UpdateProductionStandardRequest, ApiResponse<ProductionStandardDto>>($"{BaseUrl}/{id}", request);
            return response ?? ApiResponse<ProductionStandardDto>.Fail("更新失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<ProductionStandardDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除产品标准
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