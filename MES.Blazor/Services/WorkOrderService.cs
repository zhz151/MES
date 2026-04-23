using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 工单前端服务
/// </summary>
public class WorkOrderService
{
    private readonly AuthHttpClient _http;
    private const string BaseUrl = "api/workorder";

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

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<OrderWorkOrderStatusDto>>>(url);
            return response ?? ApiResponse<PagedResult<OrderWorkOrderStatusDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<OrderWorkOrderStatusDto>>.Fail($"网络错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取"订单已取消-工单待删除"列表
    /// </summary>
    public async Task<ApiResponse<List<CancelledOrderDto>>> GetCancelledOrdersAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<CancelledOrderDto>>>($"{BaseUrl}/cancelled-orders");
            return response ?? ApiResponse<List<CancelledOrderDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<CancelledOrderDto>>.Fail($"网络错误: {ex.Message}");
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
    /// 分页查询工单列表
    /// </summary>
    public async Task<ApiResponse<PagedResult<WorkOrderListDto>>> GetPagedAsync(WorkOrderQueryParams query)
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
            if (!string.IsNullOrEmpty(query.MaterialName))
                url += $"&materialName={Uri.EscapeDataString(query.MaterialName)}";
            if (!string.IsNullOrEmpty(query.Specification))
                url += $"&specification={Uri.EscapeDataString(query.Specification)}";
            if (query.DeliveryDateStart.HasValue)
                url += $"&deliveryDateStart={query.DeliveryDateStart.Value:yyyy-MM-dd}";
            if (query.DeliveryDateEnd.HasValue)
                url += $"&deliveryDateEnd={query.DeliveryDateEnd.Value:yyyy-MM-dd}";

            var response = await _http.GetFromJsonAsync<ApiResponse<PagedResult<WorkOrderListDto>>>(url);
            return response ?? ApiResponse<PagedResult<WorkOrderListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<WorkOrderListDto>>.Fail($"网络错误: {ex.Message}");
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
    /// 根据订单号获取工单列表
    /// </summary>
    public async Task<ApiResponse<List<WorkOrderListDto>>> GetBySalesOrderNoAsync(string salesOrderNo)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<WorkOrderListDto>>>($"{BaseUrl}/by-order/{Uri.EscapeDataString(salesOrderNo)}");
            return response ?? ApiResponse<List<WorkOrderListDto>>.Fail("获取数据失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<WorkOrderListDto>>.Fail($"网络错误: {ex.Message}");
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
    /// 删除工单（软删除）
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
    /// 软删除工单（用于"订单已取消-工单待删除"区域）
    /// </summary>
    public async Task<ApiResponse<object>> SoftDeleteAsync(int id)
    {
        try
        {
            var response = await _http.PostAsJsonAsync<object, ApiResponse<object>>($"{BaseUrl}/{id}/soft-delete", new { });
            return response ?? ApiResponse<object>.Fail("软删除失败");
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
        var url = $"{BaseUrl}/order-relation?salesOrderNo={Uri.EscapeDataString(salesOrderNo)}";
        var response = await _http.GetFromJsonAsync<ApiResponse<OrderWorkOrderRelationDto>>(url);
        return response ?? ApiResponse<OrderWorkOrderRelationDto>.Fail("获取数据失败");
    }
    catch (Exception ex)
    {
        return ApiResponse<OrderWorkOrderRelationDto>.Fail($"网络错误: {ex.Message}");
    }
}
    #endregion
}