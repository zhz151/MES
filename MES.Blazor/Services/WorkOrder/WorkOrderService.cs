using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Models;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.WorkOrder;
using MES.Core.DTOs.Infrastructure;

namespace MES.Blazor.Services;

/// <summary>
/// 工单前端服务
/// </summary>
public class WorkOrderService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = ApiEndpoints.WorkOrder;

    public WorkOrderService(AuthHttpClient http)
    {
        _http = http;
    }

    #region 工单首页（订单状态监控）

    /// <summary>
    /// 获取工单首页订单列表（含工单状态）
    /// </summary>
    public async Task<ApiResponse<PagedResult<OrderWorkOrderStatusDto>>> GetOrderWorkOrderStatusPageAsync(WorkOrderQueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/order-status?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (!string.IsNullOrEmpty(query.SalesOrderNo))
                url += $"&salesOrderNo={Uri.EscapeDataString(query.SalesOrderNo)}";
            if (!string.IsNullOrEmpty(query.Salesman))
                url += $"&salesman={Uri.EscapeDataString(query.Salesman)}";
            if (!string.IsNullOrEmpty(query.EndCustomer))
                url += $"&endCustomer={Uri.EscapeDataString(query.EndCustomer)}";
            if (!string.IsNullOrEmpty(query.WorkOrderStatus))
                url += $"&workOrderStatus={Uri.EscapeDataString(query.WorkOrderStatus)}";

            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<OrderWorkOrderStatusDto>>>(url);
            return response ?? ApiResponse<PagedResult<OrderWorkOrderStatusDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<OrderWorkOrderStatusDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取所有工单首页订单状态数据（无分页，供客户端筛选排序）
    /// </summary>
    public async Task<ApiResponse<List<OrderWorkOrderStatusDto>>> GetAllOrderStatusAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<OrderWorkOrderStatusDto>>>($"{BaseUrl}/order-status-all");
            return response ?? ApiResponse<List<OrderWorkOrderStatusDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<OrderWorkOrderStatusDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取已确认但无工单的订单列表（待生成工单）
    /// </summary>
    public async Task<ApiResponse<List<WorkOrderListItemDto>>> GetPendingOrdersAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<WorkOrderListItemDto>>>($"{BaseUrl}/pending-orders");
            return response ?? ApiResponse<List<WorkOrderListItemDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<WorkOrderListItemDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    #endregion

    #region 工单生成

    /// <summary>
    /// 获取待生成工单的订单项次列表
    /// </summary>
    public async Task<ApiResponse<List<OrderItemForWorkOrderDto>>> GetOrderItemsForWorkOrderAsync(string salesOrderNo)
    {
        try
        {
            var url = $"{BaseUrl}/items-for-generation?salesOrderNo={Uri.EscapeDataString(salesOrderNo)}";
            var response = await _http.GetFromJsonAsync<ApiResponse<List<OrderItemForWorkOrderDto>>>(url);
            return response ?? ApiResponse<List<OrderItemForWorkOrderDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<OrderItemForWorkOrderDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 生成工单
    /// </summary>
    public async Task<ApiResponse<List<GeneratedWorkOrderDto>>> GenerateWorkOrdersAsync(CreateWorkOrderRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<CreateWorkOrderRequest, ApiResponse<List<GeneratedWorkOrderDto>>>($"{BaseUrl}/generate", request);
            return response ?? ApiResponse<List<GeneratedWorkOrderDto>>.Fail("生成工单失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<GeneratedWorkOrderDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    #endregion

    #region 工单管理

    /// <summary>
    /// 分页查询工单列表（精简版，不含用料计划聚合数据）
    /// </summary>
    public async Task<ApiResponse<PagedResult<WorkOrderListItemDto>>> GetPagedAsync(WorkOrderQueryParams query)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={query.PageIndex}&pageSize={query.PageSize}&sortBy={Uri.EscapeDataString(query.SortBy)}&isDescending={query.IsDescending}";
            if (!string.IsNullOrEmpty(query.Keyword))
                url += $"&keyword={Uri.EscapeDataString(query.Keyword)}";
            if (!string.IsNullOrEmpty(query.SalesOrderNo))
                url += $"&salesOrderNo={Uri.EscapeDataString(query.SalesOrderNo)}";
            if (!string.IsNullOrEmpty(query.ProductionMainNo))
                url += $"&productionMainNo={Uri.EscapeDataString(query.ProductionMainNo)}";
            if (!string.IsNullOrEmpty(query.ProductionSubNo))
                url += $"&productionSubNo={Uri.EscapeDataString(query.ProductionSubNo)}";
            if (query.Status.HasValue)
                url += $"&status={query.Status.Value}";
            if (!string.IsNullOrEmpty(query.Specification))
                url += $"&specification={Uri.EscapeDataString(query.Specification)}";
            if (query.DeliveryDateStart.HasValue)
                url += $"&deliveryDateStart={query.DeliveryDateStart.Value:yyyy-MM-dd}";
            if (query.DeliveryDateEnd.HasValue)
                url += $"&deliveryDateEnd={query.DeliveryDateEnd.Value:yyyy-MM-dd}";
            if (query.Filters is { Count: > 0 }) url += $"&filters={Uri.EscapeDataString(JsonSerializer.Serialize(query.Filters))}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<WorkOrderListItemDto>>>(url);
            return response ?? ApiResponse<PagedResult<WorkOrderListItemDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<WorkOrderListItemDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据ID获取工单详情
    /// </summary>
    public async Task<ApiResponse<WorkOrderDetailDto>> GetByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<WorkOrderDetailDto>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<WorkOrderDetailDto>.Fail("获取工单详情失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<WorkOrderDetailDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据工单号获取工单详情
    /// </summary>
    public async Task<ApiResponse<WorkOrderDetailDto>> GetByWorkOrderNoAsync(string workOrderNo)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<WorkOrderDetailDto>>($"{BaseUrl}/by-workorder-no/{Uri.EscapeDataString(workOrderNo)}");
            return response ?? ApiResponse<WorkOrderDetailDto>.Fail("获取工单详情失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<WorkOrderDetailDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据订单号获取工单列表（精简 DTO，仅含 Id/工单号等基础字段）
    /// </summary>
    public async Task<ApiResponse<List<WorkOrderListItemDto>>> GetBySalesOrderNoAsync(string salesOrderNo)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<WorkOrderListItemDto>>>($"{BaseUrl}/by-order/{Uri.EscapeDataString(salesOrderNo)}");
            return response ?? ApiResponse<List<WorkOrderListItemDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<WorkOrderListItemDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取工单包含的原始订单项次列表
    /// </summary>
    public async Task<ApiResponse<List<OrderItemForWorkOrderDto>>> GetWorkOrderItemsAsync(int workOrderId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<OrderItemForWorkOrderDto>>>($"{BaseUrl}/{workOrderId}/order-items");
            return response ?? ApiResponse<List<OrderItemForWorkOrderDto>>.Fail("获取项次明细失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<OrderItemForWorkOrderDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新工单状态
    /// </summary>
    public async Task<ApiResponse<UpdateWorkOrderStatusResponseDto>> UpdateStatusAsync(int id, UpdateWorkOrderStatusRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync<UpdateWorkOrderStatusRequest, ApiResponse<UpdateWorkOrderStatusResponseDto>>($"{BaseUrl}/{id}/status", request);
            return response ?? ApiResponse<UpdateWorkOrderStatusResponseDto>.Fail("更新状态失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<UpdateWorkOrderStatusResponseDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除工单（物理删除）
    /// </summary>
    public async Task<ApiResponse<object>> DeleteAsync(int id)
    {
        try
        {
            var response = await _http.DeleteFromJsonAsync<ApiResponse<object>>($"{BaseUrl}/{id}");
            return response ?? ApiResponse<object>.Fail("删除工单失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 即时检测所有订单变更，更新工单状态
    /// </summary>
    public async Task<ApiResponse<object>> CheckAllOrderChangeAsync()
    {
        try
        {
            var response = await _http.PostAsJsonAsync<object, ApiResponse<object>>($"{BaseUrl}/check-all-order-change", new { });
            return response ?? ApiResponse<object>.Fail("操作失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除工单（用于"订单已取消-工单待删除"区域）
    /// </summary>
    public async Task<ApiResponse<object>> SoftDeleteAsync(int id)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<object, ApiResponse<object>>($"{BaseUrl}/{id}/soft-delete", new { });
            return response ?? ApiResponse<object>.Fail("删除失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail($"网络错误: {ex.Message}");
        }
    }
    /// <summary>
    /// 获取订单的工单项次追溯关系（包含该订单下所有工单及其项次明细）
    /// </summary>
    public async Task<ApiResponse<OrderWorkOrderRelationDto>> GetOrderWorkOrderRelationAsync(string salesOrderNo)
    {
        try
        {
            var url = $"{BaseUrl}/order-relation/{Uri.EscapeDataString(salesOrderNo)}";
            var response = await _http.GetFromJsonAsync<ApiResponse<OrderWorkOrderRelationDto>>(url);
            return response ?? ApiResponse<OrderWorkOrderRelationDto>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderWorkOrderRelationDto>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 全量刷新用料计划读模型
    /// </summary>
    public async Task<ApiResponse> RefreshMaterialPlanReadModelAsync()
    {
        try
        {
            var response = await _http.PostAsJsonAsync<object, ApiResponse>($"{BaseUrl}/refresh-material-plan-readmodel", new { });
            return response ?? ApiResponse.Fail("刷新失败");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 分页查询工单列表（简化参数版本，用于 ServerData 模式，不含用料计划数据）
    /// </summary>
    public async Task<ApiResponse<PagedResult<WorkOrderListItemDto>>> GetPagedAsync(
        int pageIndex = 1, int pageSize = 20, string? keyword = null,
        string? sortBy = null, bool isDescending = true, string? filters = null,
        DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        try
        {
            var url = $"{BaseUrl}/list?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            if (dateFrom.HasValue) url += $"&signDateFrom={dateFrom.Value:yyyy-MM-dd}";
            if (dateTo.HasValue) url += $"&signDateTo={dateTo.Value:yyyy-MM-dd}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<WorkOrderListItemDto>>>(url);
            return response ?? ApiResponse<PagedResult<WorkOrderListItemDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<WorkOrderListItemDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 分页查询工单列表（含用料计划聚合数据，供用料计划总览页使用）
    /// </summary>
    public async Task<ApiResponse<PagedResult<WorkOrderListDto>>> GetPagedWithPlansAsync(
        int pageIndex = 1, int pageSize = 20, string? keyword = null,
        string? sortBy = null, bool isDescending = true, string? filters = null,
        string? planTypeFilter = null,
        DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        try
        {
            var url = $"{BaseUrl}/list-with-plans?pageIndex={pageIndex}&pageSize={pageSize}&isDescending={isDescending.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
            if (!string.IsNullOrEmpty(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            if (!string.IsNullOrEmpty(filters)) url += $"&filters={Uri.EscapeDataString(filters)}";
            if (!string.IsNullOrEmpty(planTypeFilter)) url += $"&planTypeFilter={Uri.EscapeDataString(planTypeFilter)}";
            if (dateFrom.HasValue) url += $"&signDateFrom={dateFrom.Value:yyyy-MM-dd}";
            if (dateTo.HasValue) url += $"&signDateTo={dateTo.Value:yyyy-MM-dd}";
            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<WorkOrderListDto>>>(url);
            return response ?? ApiResponse<PagedResult<WorkOrderListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<WorkOrderListDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取工单筛选上下文（各列去重值）
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
    /// 获取所有用料计划总览数据（无分页，客户端筛选排序）
    /// </summary>
    public async Task<ApiResponse<List<WorkOrderListDto>>> GetAllAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<WorkOrderListDto>>>($"{BaseUrl}/list-all");
            return response ?? ApiResponse<List<WorkOrderListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<WorkOrderListDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<OperationLogDto>>> GetOperationLogsAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<OperationLogDto>>>($"{BaseUrl}/{id}/operation-logs");
            return response ?? ApiResponse<List<OperationLogDto>>.Fail("获取操作日志失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<OperationLogDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    #endregion
}