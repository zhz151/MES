// 文件路径: MES.Blazor/Services/GradeMappingService.cs
using System.Text.Json;
using MES.Core.DTOs;
using MES.Shared.Constants;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class GradeMappingService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.GradeMapping;

    public GradeMappingService(AuthHttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// 分页查询牌号对照列表（支持关键字搜索）
    /// </summary>
    public async Task<ApiResponse<PagedResult<StandardGradeMappingDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
            {
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            }
            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<StandardGradeMappingDto>>>(url);
            return response ?? ApiResponse<PagedResult<StandardGradeMappingDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<StandardGradeMappingDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取所有牌号对照（用于下拉框）
    /// </summary>
    public async Task<List<StandardGradeMappingDto>> GetAllAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<StandardGradeMappingDto>>>($"{BaseUrl}/all");
            if (response != null && response.Success && response.Data != null)
            {
                return response.Data;
            }
            return new List<StandardGradeMappingDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAllAsync error: {ex.Message}");
            return new List<StandardGradeMappingDto>();
        }
    }

    /// <summary>
    /// 根据ID获取牌号对照详情
    /// </summary>
    public async Task<ApiResponse<StandardGradeMappingDto>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<StandardGradeMappingDto>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<StandardGradeMappingDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<StandardGradeMappingDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据标准牌号获取牌号对照
    /// </summary>
    public async Task<StandardGradeMappingDto?> GetByStandardGradeAsync(string standardGrade)
    {
        var all = await GetAllAsync();
        return all.FirstOrDefault(x => x.StandardGrade == standardGrade);
    }

    /// <summary>
    /// 创建牌号对照
    /// </summary>
    public async Task<ApiResponse<StandardGradeMappingDto>> CreateAsync(CreateGradeMappingRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<CreateGradeMappingRequest, ApiResponse<StandardGradeMappingDto>>(BaseUrl, request);
            return response ?? ApiResponse<StandardGradeMappingDto>.Fail("创建失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<StandardGradeMappingDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新牌号对照
    /// </summary>
    public async Task<ApiResponse<StandardGradeMappingDto>> UpdateAsync(int id, UpdateGradeMappingRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<UpdateGradeMappingRequest, ApiResponse<StandardGradeMappingDto>>($"{BaseUrl}/{id}", request);
            return response ?? ApiResponse<StandardGradeMappingDto>.Fail("更新失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<StandardGradeMappingDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除牌号对照
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

    // ========== 筛选上下文 ==========

    /// <summary>
    /// 获取筛选上下文（各列可选值列表）
    /// </summary>
    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts")
                   ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex) { return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 打印 ==========

    /// <summary>打印单个牌号对照</summary>
    public async Task<ApiResponse<string>> PrintGradeMappingAsync(int id)
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

    /// <summary>批量打印牌号对照</summary>
    public async Task<ApiResponse<string>> PrintGradeMappingBatchAsync(int[] ids)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<OrderPrintBatchRequest, ApiResponse<string>>(
                $"{BaseUrl}/print-batch", new OrderPrintBatchRequest { Ids = ids });
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>按筛选条件打印全部牌号对照</summary>
    public async Task<ApiResponse<string>> PrintGradeMappingAllAsync(string? keyword = null, string? sortBy = null, bool isDescending = false)
    {
        try
        {
            var request = new OrderPrintAllRequest { Keyword = keyword, SortBy = sortBy, IsDescending = isDescending };
            var response = await _http.PostAsJsonAsync<OrderPrintAllRequest, ApiResponse<string>>(
                $"{BaseUrl}/print-all", request);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail($"网络错误: {ex.Message}");
        }
    }
}