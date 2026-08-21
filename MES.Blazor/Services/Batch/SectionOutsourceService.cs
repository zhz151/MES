using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Shared;

namespace MES.Blazor.Services;

public class SectionOutsourceService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.SectionOutsource;

    public SectionOutsourceService(AuthHttpClient http) => _http = http;

    // ========== 工段委外 ==========

    public async Task<ApiResponse<List<SectionOutsourceDto>>> GetByIdsAsync(int[] ids)
    {
        try
        {
            var idStr = string.Join(",", ids);
            var url = $"{BaseUrl}/by-ids?ids={Uri.EscapeDataString(idStr)}";
            return await _http.GetFromJsonAsync<ApiResponse<List<SectionOutsourceDto>>>(url)
                   ?? ApiResponse<List<SectionOutsourceDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<SectionOutsourceDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PagedResult<SectionOutsourceDto>>> GetPagedAsync(
        int pageIndex = 1, int pageSize = 20, string? keyword = null,
        string? sortBy = null, bool isDescending = true,
        DateTime? sendOutDateFrom = null, DateTime? sendOutDateTo = null,
        DateTime? actualRecoveryDateFrom = null, DateTime? actualRecoveryDateTo = null,
        string? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (sendOutDateFrom.HasValue) url += $"&sendOutDateFrom={sendOutDateFrom.Value:yyyy-MM-dd}";
            if (sendOutDateTo.HasValue) url += $"&sendOutDateTo={sendOutDateTo.Value:yyyy-MM-dd}";
            if (actualRecoveryDateFrom.HasValue) url += $"&actualRecoveryDateFrom={actualRecoveryDateFrom.Value:yyyy-MM-dd}";
            if (actualRecoveryDateTo.HasValue) url += $"&actualRecoveryDateTo={actualRecoveryDateTo.Value:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<SectionOutsourceDto>>>(url)
                   ?? ApiResponse<PagedResult<SectionOutsourceDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<SectionOutsourceDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<SectionOutsourceDto>> CreateAsync(CreateSectionOutsourceRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateSectionOutsourceRequest, ApiResponse<SectionOutsourceDto>>(BaseUrl, request)
                   ?? ApiResponse<SectionOutsourceDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<SectionOutsourceDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<SectionOutsourceDto>>> BatchCreateAsync(List<CreateSectionOutsourceRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateSectionOutsourceRequest>, ApiResponse<List<SectionOutsourceDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<SectionOutsourceDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<SectionOutsourceDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<SectionOutsourceDto>> UpdateAsync(int id, UpdateSectionOutsourceRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateSectionOutsourceRequest, ApiResponse<SectionOutsourceDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<SectionOutsourceDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<SectionOutsourceDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> DeleteAsync(int id)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<object>.Fail("删除失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 委外回收 ==========

    public async Task<ApiResponse<List<OutsourceRecoveryDto>>> GetRecoveriesAsync(int outsourceId)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<OutsourceRecoveryDto>>>($"{BaseUrl}/{outsourceId}/recoveries")
                   ?? ApiResponse<List<OutsourceRecoveryDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<OutsourceRecoveryDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<PagedResult<OutsourceRecoveryDto>>> GetRecoveriesPagedAsync(
        int pageIndex = 1, int pageSize = 20, string? keyword = null,
        string? sortBy = null, bool isDescending = true,
        DateTime? recoveryDateFrom = null, DateTime? recoveryDateTo = null,
        string? filters = null)
    {
        try
        {
            var url = $"{BaseUrl}/recoveries/list?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (recoveryDateFrom.HasValue) url += $"&recoveryDateFrom={recoveryDateFrom.Value:yyyy-MM-dd}";
            if (recoveryDateTo.HasValue) url += $"&recoveryDateTo={recoveryDateTo.Value:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<OutsourceRecoveryDto>>>(url)
                   ?? ApiResponse<PagedResult<OutsourceRecoveryDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<OutsourceRecoveryDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<OutsourceRecoveryDto>> CreateRecoveryAsync(CreateOutsourceRecoveryRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateOutsourceRecoveryRequest, ApiResponse<OutsourceRecoveryDto>>($"{BaseUrl}/recovery", request)
                   ?? ApiResponse<OutsourceRecoveryDto>.Fail("创建回收失败");
        }
        catch (Exception ex) { return ApiResponse<OutsourceRecoveryDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<OutsourceRecoveryDto>>> BatchCreateRecoveriesAsync(List<CreateOutsourceRecoveryRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateOutsourceRecoveryRequest>, ApiResponse<List<OutsourceRecoveryDto>>>($"{BaseUrl}/recoveries/batch", requests)
                   ?? ApiResponse<List<OutsourceRecoveryDto>>.Fail("批量创建回收失败");
        }
        catch (Exception ex) { return ApiResponse<List<OutsourceRecoveryDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<OutsourceRecoveryDto>> UpdateRecoveryAsync(int id, UpdateOutsourceRecoveryRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateOutsourceRecoveryRequest, ApiResponse<OutsourceRecoveryDto>>($"{BaseUrl}/recovery/{id}", request)
                   ?? ApiResponse<OutsourceRecoveryDto>.Fail("更新回收失败");
        }
        catch (Exception ex) { return ApiResponse<OutsourceRecoveryDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> DeleteRecoveryAsync(int id)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/recovery/{id}")
                   ?? ApiResponse<object>.Fail("删除回收失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 按批次查询待回收记录 ==========

    /// <summary>
    /// 根据批次号和工段名查询待回收的委外记录
    /// </summary>
    public async Task<ApiResponse<List<SectionOutsourceDto>>> GetPendingByBatchAsync(string batchNo, string sectionName)
    {
        try
        {
            var url = $"{BaseUrl}/pending-by-batch?batchNo={Uri.EscapeDataString(batchNo)}&sectionName={Uri.EscapeDataString(sectionName)}";
            return await _http.GetFromJsonAsync<ApiResponse<List<SectionOutsourceDto>>>(url)
                   ?? ApiResponse<List<SectionOutsourceDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<SectionOutsourceDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 搜索委外单位（MudAutocomplete）==========

    public async Task<List<string>> SearchVendorsAsync(string? keyword)
    {
        try
        {
            var url = $"{BaseUrl}/vendors";
            if (!string.IsNullOrWhiteSpace(keyword))
                url += $"?keyword={Uri.EscapeDataString(keyword)}";
            var response = await _http.GetFromJsonAsync<ApiResponse<List<string>>>(url);
            return response?.Data ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

// ========== 筛选上下文 ==========

    /// <summary>
    /// 获取工段委外发出筛选上下文
    /// </summary>
    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetFilterContextsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/filter-contexts");
            return response ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取筛选上下文失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取委外回收筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    public async Task<ApiResponse<Dictionary<string, List<string>>>> GetRecoveryFilterContextsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Dictionary<string, List<string>>>>($"{BaseUrl}/recoveries/filter-contexts");
            return response ?? ApiResponse<Dictionary<string, List<string>>>.Fail("获取回收筛选上下文失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<Dictionary<string, List<string>>>.Fail($"网络错误: {ex.Message}");
        }
    }

    // ========== 汇总 ==========

    /// <summary>
    /// 月度委外数据汇总（不含厂内单位）：发/回/退按 (委外单位 × 工段) 按月聚合 + 合计行
    /// </summary>
    public async Task<ApiResponse<List<SectionOutsourceMonthlyRowDto>>> GetMonthlyOutsourceAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<SectionOutsourceMonthlyRowDto>>>($"{BaseUrl}/monthly-summary")
                   ?? ApiResponse<List<SectionOutsourceMonthlyRowDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<SectionOutsourceMonthlyRowDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    /// <summary>
    /// 获取厂内单位集合（IsInternal=true 的委外单位，用于实时委外在产/月度委外数据过滤）
    /// </summary>
    public async Task<ApiResponse<List<string>>> GetInternalVendorsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<string>>>($"{BaseUrl}/internal-vendors")
                   ?? ApiResponse<List<string>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<string>>.Fail($"网络错误: {ex.Message}"); }
    }
}
