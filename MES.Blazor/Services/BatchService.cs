using System.Text.Json;
using MES.Core.DTOs;
using MES.Shared.Constants;
using MES.Core.Models;

namespace MES.Blazor.Services;

public class BatchService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.Batch;

    public BatchService(AuthHttpClient http) => _http = http;

    public async Task<ApiResponse<PagedResult<ProductionBatchListDto>>> GetPagedAsync(BatchQueryParams query)
    {
        try
        {
            var isDescending = query.IsDescending ? "true" : "false";
            var encodedSortBy = Uri.EscapeDataString(query.SortBy ?? ApiEndpoints.DefaultSortBy);
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={encodedSortBy}&isDescending={isDescending}";
            if (!string.IsNullOrEmpty(query.Keyword)) url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (!string.IsNullOrEmpty(query.WorkOrderNo)) url += $"&workOrderNo={Uri.EscapeDataString(query.WorkOrderNo)}";
            if (!string.IsNullOrEmpty(query.Status)) url += $"&status={Uri.EscapeDataString(query.Status)}";
            if (!string.IsNullOrEmpty(query.ValidInputQuestion)) url += $"&validInputQuestion={Uri.EscapeDataString(query.ValidInputQuestion)}";
            if (!string.IsNullOrEmpty(query.TagNo)) url += $"&tagNo={Uri.EscapeDataString(query.TagNo)}";
            if (!string.IsNullOrEmpty(query.BatchNo)) url += $"&batchNo={Uri.EscapeDataString(query.BatchNo)}";
            if (query.StartDateFrom.HasValue) url += $"&startDateFrom={query.StartDateFrom:yyyy-MM-dd}";
            if (query.StartDateTo.HasValue) url += $"&startDateTo={query.StartDateTo:yyyy-MM-dd}";
            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<ProductionBatchListDto>>>(url)
                   ?? ApiResponse<PagedResult<ProductionBatchListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<ProductionBatchListDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<List<ProductionBatchListDto>> GetAllBatchListAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<ProductionBatchListDto>>>($"{BaseUrl}/all-list");
            return response?.Data ?? new List<ProductionBatchListDto>();
        }
        catch
        {
            return new List<ProductionBatchListDto>();
        }
    }

    public async Task<ApiResponse<ProductionBatchDetailDto>> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<ProductionBatchDetailDto>>($"{BaseUrl}/{id}")
                   ?? ApiResponse<ProductionBatchDetailDto>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<ProductionBatchDetailDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<ProductionBatchDetailDto>> GetByBatchNoAsync(string batchNo)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<ProductionBatchDetailDto>>($"{BaseUrl}/by-batch-no/{Uri.EscapeDataString(batchNo)}")
                   ?? ApiResponse<ProductionBatchDetailDto>.Fail("获取批次信息失败");
        }
        catch (Exception ex) { return ApiResponse<ProductionBatchDetailDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<AdjacentBatchDto>> GetAdjacentBatchAsync(int currentId)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<AdjacentBatchDto>>($"{BaseUrl}/{currentId}/adjacent")
                   ?? ApiResponse<AdjacentBatchDto>.Fail("获取导航信息失败");
        }
        catch (Exception ex) { return ApiResponse<AdjacentBatchDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<ProductionBatchListDto>> CreateAsync(CreateProductionBatchRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateProductionBatchRequest, ApiResponse<ProductionBatchListDto>>(BaseUrl, request)
                   ?? ApiResponse<ProductionBatchListDto>.Fail("创建失败");
        }
        catch (Exception ex) { return ApiResponse<ProductionBatchListDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<ProductionBatchDetailDto>> UpdateAsync(int id, UpdateProductionBatchRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateProductionBatchRequest, ApiResponse<ProductionBatchDetailDto>>($"{BaseUrl}/{id}", request)
                   ?? ApiResponse<ProductionBatchDetailDto>.Fail("更新失败");
        }
        catch (Exception ex) { return ApiResponse<ProductionBatchDetailDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> UpdateStatusAsync(int id, UpdateBatchStatusRequest request)
    {
        try
        {
            return await _http.PutAsJsonAsync<UpdateBatchStatusRequest, ApiResponse<object>>($"{BaseUrl}/{id}/status", request)
                   ?? ApiResponse<object>.Fail("状态更新失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
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

    public async Task<ApiResponse<SaveBatchResponse>> SaveAllAsync(int id, SaveBatchRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<SaveBatchRequest, ApiResponse<SaveBatchResponse>>(
                $"{BaseUrl}/{id}/save-all", request);
            return response ?? ApiResponse<SaveBatchResponse>.Fail("批量保存失败");
        }
        catch (Exception ex) { return ApiResponse<SaveBatchResponse>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 编号生成 ==========

    public async Task<ApiResponse<string>> GetNextBatchNoAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<string>>($"{BaseUrl}/next-batch-no");
            return response ?? ApiResponse<string>.Fail("获取编号失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 查询 ==========

    public async Task<ApiResponse<List<AvailableBatchDto>>> GetAvailableBatchesAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<AvailableBatchDto>>>($"{BaseUrl}/available-batches");
            return response ?? ApiResponse<List<AvailableBatchDto>>.Fail("获取可用批次失败");
        }
        catch (Exception ex) { return ApiResponse<List<AvailableBatchDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 工序组 ==========

    public async Task<ApiResponse<List<ProcessGroupDto>>> GetProcessGroupsAsync(int batchId)
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiResponse<List<ProcessGroupDto>>>($"{BaseUrl}/{batchId}/records")
                   ?? ApiResponse<List<ProcessGroupDto>>.Fail("获取工序组失败");
        }
        catch (Exception ex) { return ApiResponse<List<ProcessGroupDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<ProcessGroupDto>> AddProcessGroupAsync(int batchId, CreateProcessGroupRequest request)
    {
        try
        {
            return await _http.PostAsJsonAsync<CreateProcessGroupRequest, ApiResponse<ProcessGroupDto>>($"{BaseUrl}/{batchId}/records", request)
                   ?? ApiResponse<ProcessGroupDto>.Fail("添加工序组失败");
        }
        catch (Exception ex) { return ApiResponse<ProcessGroupDto>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<object>> DeleteProcessGroupAsync(int recordId)
    {
        try
        {
            return await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/records/{recordId}")
                   ?? ApiResponse<object>.Fail("删除工序组失败");
        }
        catch (Exception ex) { return ApiResponse<object>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 复制上个工序组 ==========

    public async Task<ApiResponse<List<CreateProcessGroupRequest>>> GetLastBatchProcessGroupsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<CreateProcessGroupRequest>>>($"{BaseUrl}/last-process-groups");
            return response ?? ApiResponse<List<CreateProcessGroupRequest>>.Fail("获取失败");
        }
        catch (Exception ex) { return ApiResponse<List<CreateProcessGroupRequest>>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 工单号验证 ==========

    public async Task<ApiResponse<List<BatchWorkOrderMismatchDto>>> VerifyWorkOrderNosAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<BatchWorkOrderMismatchDto>>>($"{BaseUrl}/verify-workorders");
            return response ?? ApiResponse<List<BatchWorkOrderMismatchDto>>.Fail("验证失败");
        }
        catch (Exception ex) { return ApiResponse<List<BatchWorkOrderMismatchDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 打印 ==========

    public async Task<ApiResponse<string>> PrintBatchAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<string>>($"{BaseUrl}/{id}/print");
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> PrintBatchAllAsync(BatchPrintAllRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<BatchPrintAllRequest, ApiResponse<string>>($"{BaseUrl}/print-all", request);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> PrintBatchSelectedAsync(int[] ids)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<int[], ApiResponse<string>>($"{BaseUrl}/print-selected", ids);
            return response ?? ApiResponse<string>.Fail("打印失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> PrintProcessCardAsync(ProcessCardPrintRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<ProcessCardPrintRequest, ApiResponse<string>>($"{BaseUrl}/print-process-card", request);
            return response ?? ApiResponse<string>.Fail("打印工艺卡失败");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 按批次号调取工序组 ==========

    public async Task<ApiResponse<List<CreateProcessGroupRequest>>> GetProcessGroupsByBatchNoAsync(string batchNo)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<CreateProcessGroupRequest>>>($"{BaseUrl}/{batchNo}/process-groups");
            return response ?? ApiResponse<List<CreateProcessGroupRequest>>.Fail("获取失败");
        }
        catch (Exception ex) { return ApiResponse<List<CreateProcessGroupRequest>>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 批次操作日志 ==========

    public async Task<ApiResponse<List<BatchOperationLogDto>>> GetOperationLogsAsync(int batchId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<BatchOperationLogDto>>>($"{BaseUrl}/{batchId}/operation-logs");
            return response ?? ApiResponse<List<BatchOperationLogDto>>.Fail("获取失败");
        }
        catch (Exception ex) { return ApiResponse<List<BatchOperationLogDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 通用查询（支持 validInputQuestion） ==========

    public async Task<ApiResponse<PagedResult<ProductionBatchListDto>>> GetAllAsync(int pageIndex, int pageSize, string? keyword, string? sortBy, bool isDescending, string? filters, string? validInputQuestion)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={pageIndex}&pageSize={pageSize}&sortBy={Uri.EscapeDataString(sortBy ?? ApiEndpoints.DefaultSortBy)}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            if (!string.IsNullOrEmpty(validInputQuestion)) url += $"&validInputQuestion={Uri.EscapeDataString(validInputQuestion)}";
            return await _http.GetFromJsonAsync<ApiResponse<PagedResult<ProductionBatchListDto>>>(url)
                   ?? ApiResponse<PagedResult<ProductionBatchListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex) { return ApiResponse<PagedResult<ProductionBatchListDto>>.Fail($"网络错误: {ex.Message}"); }
    }

    // ========== 筛选上下文 ==========

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
