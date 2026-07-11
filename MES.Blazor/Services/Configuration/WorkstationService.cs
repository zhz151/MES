using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Services;

public class WorkstationService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.Workstation;

    public WorkstationService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<WorkstationDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<WorkstationDto>>>(url);
            return response ?? ApiResponse<PagedResult<WorkstationDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<WorkstationDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<WorkstationDto>> GetByCodeAsync(string code)
    {
        try
        {
            var url = $"{BaseUrl}/{Uri.EscapeDataString(code)}";
            return await _http.GetFromJsonAsync<ApiResponse<WorkstationDto>>(url)
                   ?? ApiResponse<WorkstationDto>.Fail("请求失败");
        }
        catch (Exception ex) { return ApiResponse<WorkstationDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<bool>> SaveAsync(WorkstationDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<WorkstationDto, ApiResponse<bool>>($"{BaseUrl}/save", dto);
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
