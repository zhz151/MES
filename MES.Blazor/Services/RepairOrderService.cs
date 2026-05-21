using System.Text.Json;
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class RepairOrderService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/repair-order";

    public RepairOrderService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<RepairOrderListDto>>> GetPagedAsync(RepairOrderQueryParams query)
    {
        try
        {
            var isDescending = query.IsDescending ? "true" : "false";
            var encodedSortBy = Uri.EscapeDataString(query.SortBy ?? "ReportTime");
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={encodedSortBy}&isDescending={isDescending}";
            if (!string.IsNullOrEmpty(query.Keyword)) url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<RepairOrderListDto>>>(url)
                   ?? ApiResponse<PagedResult<RepairOrderListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<RepairOrderListDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<RepairOrderListDto>>> GetAllListAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<RepairOrderListDto>>>($"{BaseUrl}/all-list")
                   ?? ApiResponse<List<RepairOrderListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<List<RepairOrderListDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<RepairOrderListDto>> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<RepairOrderListDto>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<RepairOrderListDto>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<RepairOrderListDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<RepairOrderListDto>> CreateAsync(CreateRepairOrderRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateRepairOrderRequest, ApiResponse<RepairOrderListDto>>(BaseUrl, request)
                   ?? ApiResponse<RepairOrderListDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<RepairOrderListDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<RepairOrderListDto>>> CreateBatchAsync(List<CreateRepairOrderRequest> requests)
    {
        try
        {
            return await _http.PostAsJsonAsync<List<CreateRepairOrderRequest>, ApiResponse<List<RepairOrderListDto>>>($"{BaseUrl}/batch", requests)
                   ?? ApiResponse<List<RepairOrderListDto>>.Fail("批量创建失败");
        }
        catch (Exception ex) { return ApiResponse<List<RepairOrderListDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<RepairOrderListDto>> UpdateAsync(int id, UpdateRepairOrderRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateRepairOrderRequest, ApiResponse<RepairOrderListDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<RepairOrderListDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<RepairOrderListDto>.Fail($"网络错误: {ex.Message}"); }
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

    public async Task<ApiResponse<string>> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        try
        {
            var request = new RepairOrderPrintBatchRequest { Ids = ids, Columns = columns };
            return await _http.PostAsJsonAsync<RepairOrderPrintBatchRequest, ApiResponse<string>>($"{BaseUrl}/print-batch", request)
                   ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> PrintAllAsync(RepairOrderQueryParams query, List<PrintColumnDef> columns)
    {
        try
        {
            var request = new RepairOrderPrintAllRequest
            {
                Keyword = query.Keyword,
                SortBy = query.SortBy,
                IsDescending = query.IsDescending,
                EquipmentId = query.EquipmentId,
                RepairStatus = query.RepairStatus,
                Priority = query.Priority,
                ReportTimeFrom = query.ReportTimeFrom,
                ReportTimeTo = query.ReportTimeTo,
                Columns = columns
            };
            return await _http.PostAsJsonAsync<RepairOrderPrintAllRequest, ApiResponse<string>>($"{BaseUrl}/print-all", request)
                   ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }
}
