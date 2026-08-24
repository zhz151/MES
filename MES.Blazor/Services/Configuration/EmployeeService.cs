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

    /// <summary>
    /// 按生产工段获取启用员工（普通生产工位扫码操作人下拉），走 GetPagedAsync + SectionName 筛选
    /// </summary>
    public async Task<ApiResponse<List<EmployeeDto>>> GetBySectionAsync(string? sectionName)
        => await GetEnabledEmployeesAsync(new List<FilterDescriptor>
        {
            new() { Field = "SectionName", Operator = "equals", Value = sectionName }
        });

    /// <summary>
    /// 按工段 + 组类获取启用员工（普通生产工位扫码操作人下拉）；
    /// 组类过滤 = GroupName 逗号串任一元素精确匹配（员工可多组）；组类传空 = 不按组过滤（工位未配置组类）
    /// </summary>
    public async Task<ApiResponse<List<EmployeeDto>>> GetBySectionAndGroupAsync(string? sectionName, string? groupName)
        => await GetEnabledEmployeesAsync(new List<FilterDescriptor>
        {
            new() { Field = "SectionName", Operator = "equals", Value = sectionName },
            new() { Field = "GroupName", Operator = "equals", Value = groupName }
        });

    /// <summary>
    /// 按成检项目获取启用员工（成品检验扫码操作人下拉），走 GetPagedAsync + InspectionItems 筛选
    /// </summary>
    public async Task<ApiResponse<List<EmployeeDto>>> GetByInspectionItemAsync(string? inspectionItem)
        => await GetEnabledEmployeesAsync(new List<FilterDescriptor>
        {
            new() { Field = "InspectionItems", Operator = "equals", Value = inspectionItem }
        });

    /// <summary>
    /// 获取「过程检验」=是 的启用员工（过程检验扫码操作人下拉，不按项目/工段过滤）
    /// </summary>
    public async Task<ApiResponse<List<EmployeeDto>>> GetByProcessInspectionAsync()
        => await GetEnabledEmployeesAsync(new List<FilterDescriptor>
        {
            new() { Field = "ProcessInspectionItems", Operator = "equals", Value = "True" }
        });

    /// <summary>
    /// 获取「成检到料」=是 的启用员工（成检到料扫码确认人下拉，不按项目/工段过滤）
    /// </summary>
    public async Task<ApiResponse<List<EmployeeDto>>> GetByMaterialReceiveCheckAsync()
        => await GetEnabledEmployeesAsync(new List<FilterDescriptor>
        {
            new() { Field = "MaterialReceiveCheckItems", Operator = "equals", Value = "True" }
        });

    /// <summary>按筛选条件获取启用员工，扫码操作人下拉统一入口（空值筛选自动忽略）</summary>
    private async Task<ApiResponse<List<EmployeeDto>>> GetEnabledEmployeesAsync(List<FilterDescriptor> filters)
    {
        try
        {
            var query = new QueryParams { PageIndex = 1, PageSize = 200, SortBy = "Code" };
            var effective = filters
                .Where(f => !string.IsNullOrWhiteSpace(f.Value?.ToString()))
                .ToList();
            if (effective.Count > 0)
                query.Filters = effective;
            var result = await GetPagedAsync(query);
            if (result.Success && result.Data != null)
                return ApiResponse<List<EmployeeDto>>.Ok(result.Data.Items.Where(e => e.IsActive).ToList());
            return ApiResponse<List<EmployeeDto>>.Fail(result.Message ?? "获取员工失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<EmployeeDto>>.Fail($"网络错误: {ex.Message}");
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

    /// <summary>列头筛选上下文（ExcelFilter 下拉选项）</summary>
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
}
