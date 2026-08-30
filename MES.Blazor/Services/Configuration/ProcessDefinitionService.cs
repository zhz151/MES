using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Services;

public class ProcessDefinitionService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.ProcessDefinition;

    public ProcessDefinitionService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<ProcessDefinitionDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<ProcessDefinitionDto>>>(url);
            return response ?? ApiResponse<PagedResult<ProcessDefinitionDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<ProcessDefinitionDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<ProcessInfoDto>>> GetEnabledProcessesAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<ProcessInfoDto>>>($"{BaseUrl}/enabled-processes");
            return response ?? ApiResponse<List<ProcessInfoDto>>.Fail("获取启用工序列表失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<ProcessInfoDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>冷轧/冷拔工序选项（仅启用的 IsEnabled=true），机型下拉/工段 Tab/机台组配置工序多选动态化用</summary>
    public async Task<ApiResponse<List<ProcessInfoDto>>> GetColdRollOptionsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<ProcessInfoDto>>>($"{BaseUrl}/cold-roll-options");
            return response ?? ApiResponse<List<ProcessInfoDto>>.Fail("获取冷轧工序选项失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<ProcessInfoDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取工序 Key → 显示中文 映射（配置表优先，兜底 ProcessNames）。
    /// 失败返回 null，调用方（MainLayout）保持 ProcessDisplayHelper.OverrideMap 为 null 即可回退。
    /// </summary>
    public async Task<Dictionary<string, string>?> GetProcessNameMapAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, string>>>($"{BaseUrl}/process-name-map");
            return response?.Success == true ? response.Data : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ApiResponse<ProcessDefinitionDto>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<ProcessDefinitionDto>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<ProcessDefinitionDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<ProcessDefinitionDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveAsync(ProcessDefinitionDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<ProcessDefinitionDto, ApiResponse<bool>>($"{BaseUrl}/save", dto);
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
