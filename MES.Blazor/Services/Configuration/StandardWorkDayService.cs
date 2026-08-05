using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Services;

public class StandardWorkDayService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.StandardWorkDay;

    public StandardWorkDayService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<StandardWorkDayDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<StandardWorkDayDto>>>(url);
            return response ?? ApiResponse<PagedResult<StandardWorkDayDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<StandardWorkDayDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<SectionInfoDto>>> GetEnabledSectionsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<SectionInfoDto>>>($"{BaseUrl}/enabled-sections");
            return response ?? ApiResponse<List<SectionInfoDto>>.Fail("获取启用工段列表失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<SectionInfoDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取工段 Key → 显示中文 映射（配置表优先，兜底 SectionDefs）。
    /// 失败返回 null，调用方（MainLayout）保持 SectionDisplayHelper.OverrideMap 为 null 即可回退。
    /// </summary>
    public async Task<Dictionary<string, string>?> GetSectionNameMapAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, string>>>($"{BaseUrl}/section-name-map");
            return response?.Success == true ? response.Data : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ApiResponse<StandardWorkDayDto>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<StandardWorkDayDto>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<StandardWorkDayDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<StandardWorkDayDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveAsync(StandardWorkDayDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<StandardWorkDayDto, ApiResponse<bool>>($"{BaseUrl}/save", dto);
            return response ?? ApiResponse<bool>.Fail("保存失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<object?, ApiResponse<bool>>($"{BaseUrl}/delete/{id}", null);
            return response ?? ApiResponse<bool>.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
        }
    }
}
