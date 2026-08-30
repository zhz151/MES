using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Scheduling;

namespace MES.Blazor.Services;

/// <summary>
/// 冷轧机台组配置 Blazor 前端服务（查询 + 保存 + 删除）
/// </summary>
public class ColdRollMachineGroupConfigService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.ColdRollMachineGroupConfig;

    public ColdRollMachineGroupConfigService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<ColdRollMachineGroupConfigDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<ColdRollMachineGroupConfigDto>>>(url);
            return response ?? ApiResponse<PagedResult<ColdRollMachineGroupConfigDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<ColdRollMachineGroupConfigDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<ColdRollMachineGroupConfigDto>>> GetAllAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<ColdRollMachineGroupConfigDto>>>($"{BaseUrl}/all");
            return response ?? ApiResponse<List<ColdRollMachineGroupConfigDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<ColdRollMachineGroupConfigDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveAsync(ColdRollMachineGroupConfigDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<ColdRollMachineGroupConfigDto, ApiResponse<bool>>($"{BaseUrl}/save", dto);
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
            var response = await _http.PostAsJsonAsync<int, ApiResponse<bool>>($"{BaseUrl}/delete/{id}", id);
            return response ?? ApiResponse<bool>.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"网络错误: {ex.Message}");
        }
    }
}
