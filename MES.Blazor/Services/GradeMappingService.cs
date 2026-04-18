// 文件路径: MES.Blazor/Services/GradeMappingService.cs
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class GradeMappingService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/grade-mapping";

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
}