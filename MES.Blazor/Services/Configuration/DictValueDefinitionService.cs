using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;
using MES.Core.Helpers;

namespace MES.Blazor.Services;

/// <summary>
/// 字典值配置服务（前端）：管理 string 存储字典字段（工段/工序/紧急度/产类/流转/关注目标/汇总行/责任类别）
/// 的中文显示名、排序、隐藏与可加值。
/// GetDisplayMapAsync 返回全量 DictKey→Value→DisplayName 映射，供前端显示层/筛选覆盖使用。
/// </summary>
public class DictValueDefinitionService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.DictValueDefinition;

    public DictValueDefinitionService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<DictValueDefinitionDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<DictValueDefinitionDto>>>(url);
            return response ?? ApiResponse<PagedResult<DictValueDefinitionDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<DictValueDefinitionDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取全量显示映射：DictKey → Value → DisplayName（配置表优先，兜底 DictValueDefaults）。
    /// 失败返回 null，调用方保持覆盖为 null 即可回退。
    /// </summary>
    public async Task<Dictionary<string, Dictionary<string, string>>?> GetDisplayMapAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, Dictionary<string, string>>>>($"{BaseUrl}/display-map");
            return response?.Success == true ? response.Data : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 启用字典值列表（配置表优先 + 静态兜底），供下拉选项动态加载（如责任类型下拉）。
    /// </summary>
    public async Task<ApiResponse<List<DictValueInfoDto>>> GetEnabledValuesAsync(string dictKey)
    {
        try
        {
            var url = $"{BaseUrl}/enabled-values?key={Uri.EscapeDataString(dictKey)}";
            var response = await _http.GetFromJsonAsync<ApiResponse<List<DictValueInfoDto>>>(url);
            return response ?? ApiResponse<List<DictValueInfoDto>>.Fail("获取启用字典值失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<DictValueInfoDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> RestoreDefaultsAsync(string dictKey)
    {
        try
        {
            var url = $"{BaseUrl}/restore-defaults?key={Uri.EscapeDataString(dictKey)}";
            var response = await _http.PostAsJsonAsync<object?, ApiResponse<int>>(url, null);
            return response ?? ApiResponse<int>.Fail("恢复默认失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<DictValueDefinitionDto>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<DictValueDefinitionDto>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<DictValueDefinitionDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<DictValueDefinitionDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveAsync(DictValueDefinitionDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<DictValueDefinitionDto, ApiResponse<bool>>($"{BaseUrl}/save", dto);
            if (response?.Success == true)
                await RefreshDisplayMapAsync(); // 保存即刷新前端静态映射，SPA 内新增/改名后全站显示免 F5 生效
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
            if (response?.Success == true)
                await RefreshDisplayMapAsync(); // 删除后同步刷新前端静态映射
            return response ?? ApiResponse<bool>.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>重新拉取 display-map 并注入前端静态 DictValueDisplayHelper.OverrideMap，保证新增/删除字典值后全站显示即时生效。</summary>
    private async Task RefreshDisplayMapAsync()
    {
        var map = await GetDisplayMapAsync();
        if (map != null)
            DictValueDisplayHelper.OverrideMap = map;
    }
}
