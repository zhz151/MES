using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;
using MES.Core.Helpers;

namespace MES.Blazor.Services;

/// <summary>
/// 枚举显示配置服务（前端）：管理 C# 强类型枚举的中文显示名与排序。
/// GetDisplayMapAsync 返回全量 EnumKey→Value→DisplayName 映射，供前端显示层/筛选覆盖使用。
/// </summary>
public class EnumDisplayDefinitionService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.EnumDisplayDefinition;

    public EnumDisplayDefinitionService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<EnumDisplayDefinitionDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<EnumDisplayDefinitionDto>>>(url);
            return response ?? ApiResponse<PagedResult<EnumDisplayDefinitionDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<EnumDisplayDefinitionDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取全量显示映射：EnumKey → Value → DisplayName（配置表优先，兜底 EnumHelper）。
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
    /// 获取全量显示选项：EnumKey → 有序 (Value/DisplayName/DisplayOrder)。
    /// 失败返回 null，调用方保持未注入排序即可回退静态注册顺序。
    /// </summary>
    public async Task<Dictionary<string, List<EnumDisplayOptionDto>>?> GetOptionsMapAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<EnumDisplayOptionDto>>>>($"{BaseUrl}/options-map");
            return response?.Success == true ? response.Data : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts")
                   ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> RestoreDefaultsAsync(string enumKey)
    {
        try
        {
            var url = $"{BaseUrl}/restore-defaults?key={Uri.EscapeDataString(enumKey)}";
            var response = await _http.PostAsJsonAsync<object?, ApiResponse<int>>(url, null);
            if (response?.Success == true)
                await RefreshEnumSnapshotAsync(); // 恢复默认后同步刷新前端静态覆盖，SPA 内即时生效
            return response ?? ApiResponse<int>.Fail("恢复默认失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<EnumDisplayDefinitionDto>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<EnumDisplayDefinitionDto>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<EnumDisplayDefinitionDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<EnumDisplayDefinitionDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveAsync(EnumDisplayDefinitionDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<EnumDisplayDefinitionDto, ApiResponse<bool>>($"{BaseUrl}/save", dto);
            if (response?.Success == true)
                await RefreshEnumSnapshotAsync(); // 保存即刷新前端静态覆盖，SPA 内改名/排序后全站显示免 F5 生效
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
                await RefreshEnumSnapshotAsync(); // 删除后同步刷新前端静态覆盖
            return response ?? ApiResponse<bool>.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 重新拉取 display-map/options-map 并注入前端静态 EnumHelper 覆盖，
    /// 保证枚举改名/排序后 SPA 内全站显示即时生效（与后端 RefreshStaticSnapshotAsync、字典 RefreshDisplayMapAsync 同模式）。
    /// </summary>
    private async Task RefreshEnumSnapshotAsync()
    {
        var displayMap = await GetDisplayMapAsync();
        if (displayMap != null)
        {
            foreach (var kvp in displayMap)
                EnumHelper.ApplyEnumOverrides(kvp.Key, kvp.Value);
        }

        var optionsMap = await GetOptionsMapAsync();
        if (optionsMap != null)
        {
            foreach (var kvp in optionsMap)
            {
                var order = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var opt in kvp.Value)
                    order[opt.Value] = opt.DisplayOrder;
                EnumHelper.ApplyEnumOrder(kvp.Key, order);
            }
        }
    }
}
