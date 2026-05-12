using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class SectionOutsourceService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/section-outsource";

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
        DateTime? actualRecoveryDateFrom = null, DateTime? actualRecoveryDateTo = null)
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
        DateTime? recoveryDateFrom = null, DateTime? recoveryDateTo = null)
    {
        try
        {
            var url = $"{BaseUrl}/recoveries/list?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (recoveryDateFrom.HasValue) url += $"&recoveryDateFrom={recoveryDateFrom.Value:yyyy-MM-dd}";
            if (recoveryDateTo.HasValue) url += $"&recoveryDateTo={recoveryDateTo.Value:yyyy-MM-dd}";
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

    // ========== 打印 ==========

    public async Task<ApiResponse<string>> PrintSelectedAsync(int[] ids, List<PrintColumnDef> columns)
    {
        try
        {
            var request = new SectionOutsourcePrintBatchRequest { Ids = ids, Columns = columns };
            var response = await _http.PostAsJsonAsync<SectionOutsourcePrintBatchRequest, ApiResponse<string>>(
                $"{BaseUrl}/print-selected", request);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> PrintAllAsync(string? keyword = null, string? sortBy = null,
        bool isDescending = true, List<PrintColumnDef>? columns = null,
        DateTime? sendOutDateFrom = null, DateTime? sendOutDateTo = null,
        DateTime? actualRecoveryDateFrom = null, DateTime? actualRecoveryDateTo = null)
    {
        try
        {
            var request = new SectionOutsourcePrintAllRequest
            {
                Keyword = keyword,
                SortBy = sortBy,
                IsDescending = isDescending,
                Columns = columns ?? new(),
                SendOutDateFrom = sendOutDateFrom,
                SendOutDateTo = sendOutDateTo,
                ActualRecoveryDateFrom = actualRecoveryDateFrom,
                ActualRecoveryDateTo = actualRecoveryDateTo
            };
            var response = await _http.PostAsJsonAsync<SectionOutsourcePrintAllRequest, ApiResponse<string>>(
                $"{BaseUrl}/print-all", request);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> PrintRecoverySelectedAsync(int[] ids, List<PrintColumnDef> columns)
    {
        try
        {
            var request = new RecoveryPrintBatchRequest { Ids = ids, Columns = columns };
            var response = await _http.PostAsJsonAsync<RecoveryPrintBatchRequest, ApiResponse<string>>(
                $"{BaseUrl}/recoveries/print-selected", request);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> PrintRecoveryAllAsync(string? keyword = null, string? sortBy = null,
        bool isDescending = true, List<PrintColumnDef>? columns = null,
        DateTime? recoveryDateFrom = null, DateTime? recoveryDateTo = null)
    {
        try
        {
            var request = new RecoveryPrintAllRequest
            {
                Keyword = keyword,
                SortBy = sortBy,
                IsDescending = isDescending,
                Columns = columns ?? new(),
                RecoveryDateFrom = recoveryDateFrom,
                RecoveryDateTo = recoveryDateTo
            };
            var response = await _http.PostAsJsonAsync<RecoveryPrintAllRequest, ApiResponse<string>>(
                $"{BaseUrl}/recoveries/print-all", request);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }
}
