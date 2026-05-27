// 文件路径: MES.Blazor/Services/StandardProcessCycleService.cs
using System.Text.Json;
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class StandardProcessCycleService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/standard-process-cycle";

    public StandardProcessCycleService(AuthHttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResponse<PagedResult<StandardProcessCycleDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<StandardProcessCycleDto>>>(url);
            return response ?? ApiResponse<PagedResult<StandardProcessCycleDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<StandardProcessCycleDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<List<StandardProcessCycleDto>> GetAllAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<StandardProcessCycleDto>>>($"{BaseUrl}/all");
            if (response != null && response.Success && response.Data != null)
                return response.Data;
            return new List<StandardProcessCycleDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAllAsync error: {ex.Message}");
            return new List<StandardProcessCycleDto>();
        }
    }

    public async Task<ApiResponse<StandardProcessCycleDto>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<StandardProcessCycleDto>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<StandardProcessCycleDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<StandardProcessCycleDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<StandardProcessCycleDto>> CreateAsync(CreateStandardProcessCycleRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<CreateStandardProcessCycleRequest, ApiResponse<StandardProcessCycleDto>>(BaseUrl, request);
            return response ?? ApiResponse<StandardProcessCycleDto>.Fail("创建失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<StandardProcessCycleDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<StandardProcessCycleDto>> UpdateAsync(int id, UpdateStandardProcessCycleRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<UpdateStandardProcessCycleRequest, ApiResponse<StandardProcessCycleDto>>($"{BaseUrl}/{id}", request);
            return response ?? ApiResponse<StandardProcessCycleDto>.Fail("更新失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<StandardProcessCycleDto>.Fail($"网络错误: {ex.Message}");
        }
    }

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

    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts")
                   ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex) { return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}"); }
    }
}
