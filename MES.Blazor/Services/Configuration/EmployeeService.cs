using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Services;

public class EmployeeService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.Employee;

    public EmployeeService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<EmployeeDto>>> GetPagedAsync(QueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 })
                url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<EmployeeDto>>>(url);
            return response ?? ApiResponse<PagedResult<EmployeeDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<EmployeeDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<EmployeeDto>> GetByCodeAsync(string code)
    {
        try
        {
            var url = $"{BaseUrl}/{Uri.EscapeDataString(code)}";
            return await _http.GetFromJsonAsync<ApiResponse<EmployeeDto>>(url)
                   ?? ApiResponse<EmployeeDto>.Fail("请求失败");
        }
        catch (Exception ex) { return ApiResponse<EmployeeDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<bool>> SaveAsync(EmployeeDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<EmployeeDto, ApiResponse<bool>>($"{BaseUrl}/save", dto);
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
