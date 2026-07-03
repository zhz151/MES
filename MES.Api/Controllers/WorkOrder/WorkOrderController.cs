using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

/// <summary>
/// 工单控制器
/// </summary>
[ApiController]
[Route("api/workorder")]
[Authorize]
public class WorkOrderController : ControllerBase
{
    private readonly IWorkOrderService _workOrderService;

    public WorkOrderController(IWorkOrderService workOrderService)
    {
        _workOrderService = workOrderService;
    }

    #region 工单首页（订单状态监控）

    [HttpGet("order-status")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<OrderWorkOrderStatusDto>>>> GetOrderWorkOrderStatus(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? salesOrderNo = null,
        [FromQuery] string? productionMainNo = null,
        [FromQuery] string? productionSubNo = null,
        [FromQuery] int? status = null,
        [FromQuery] string? workOrderStatus = null,
        [FromQuery] string? materialName = null,
        [FromQuery] string? specification = null,
        [FromQuery] DateTime? deliveryDateStart = null,
        [FromQuery] DateTime? deliveryDateEnd = null,
        [FromQuery] string? salesman = null,
        [FromQuery] string? endCustomer = null,
        [FromQuery] string? plantGrade = null,
        [FromQuery] bool includeCancelled = false,
        [FromQuery] int? materialPlanStatus = null,
        [FromQuery] int? mainNoMaterialPlanStatus = null,
        [FromQuery] int? orderMaterialPlanStatus = null,
        [FromQuery] string? planTypeFilter = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        WorkOrderQueryParams query = new()
        {
            PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy, IsDescending = isDescending,
            SalesOrderNo = salesOrderNo, ProductionMainNo = productionMainNo, ProductionSubNo = productionSubNo,
            Status = status, WorkOrderStatus = workOrderStatus, MaterialName = materialName,
            Specification = specification, DeliveryDateStart = deliveryDateStart, DeliveryDateEnd = deliveryDateEnd,
            Salesman = salesman, EndCustomer = endCustomer, PlantGrade = plantGrade,
            IncludeCancelled = includeCancelled, MaterialPlanStatus = materialPlanStatus,
            MainNoMaterialPlanStatus = mainNoMaterialPlanStatus, OrderMaterialPlanStatus = orderMaterialPlanStatus,
            PlanTypeFilter = planTypeFilter
        };
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _workOrderService.GetOrderWorkOrderStatusPageAsync(query);
        return Ok(ApiResponse<PagedResult<OrderWorkOrderStatusDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("order-status-all")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<OrderWorkOrderStatusDto>>>> GetAllOrderStatusList()
    {
        var result = await _workOrderService.GetAllOrderStatusListAsync();
        return Ok(ApiResponse<List<OrderWorkOrderStatusDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("cancelled-orders")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<CancelledOrderDto>>>> GetCancelledOrders()
    {
        var result = await _workOrderService.GetCancelledOrdersAsync();
        return Ok(ApiResponse<List<CancelledOrderDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("pending-orders")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<WorkOrderListItemDto>>>> GetPendingOrders()
    {
        var result = await _workOrderService.GetPendingOrdersAsync();
        return Ok(ApiResponse<List<WorkOrderListItemDto>>.Ok(result, "查询成功"));
    }

    #endregion

    #region 工单生成

    [HttpGet("items-for-generation")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<OrderItemForWorkOrderDto>>>> GetOrderItemsForWorkOrder(
        [FromQuery] string salesOrderNo)
    {
        if (string.IsNullOrWhiteSpace(salesOrderNo))
            return BadRequest(ApiResponse<List<OrderItemForWorkOrderDto>>.Fail("订单号不能为空"));

        var result = await _workOrderService.GetOrderItemsForWorkOrderAsync(salesOrderNo);
        return Ok(ApiResponse<List<OrderItemForWorkOrderDto>>.Ok(result, "查询成功"));
    }

    [HttpPost("generate")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<GeneratedWorkOrderDto>>>> GenerateWorkOrders(
        [FromBody] CreateWorkOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<GeneratedWorkOrderDto>>.Fail("请求参数无效"));

        var result = await _workOrderService.GenerateWorkOrdersAsync(request);
        return Ok(ApiResponse<List<GeneratedWorkOrderDto>>.Ok(result, "生成成功"));
    }

    #endregion

    #region 工单管理

    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkOrderListItemDto>>>> GetList(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? salesOrderNo = null,
        [FromQuery] string? productionMainNo = null,
        [FromQuery] string? productionSubNo = null,
        [FromQuery] int? status = null,
        [FromQuery] string? specification = null,
        [FromQuery] string? salesman = null,
        [FromQuery] string? endCustomer = null,
        [FromQuery] string? plantGrade = null,
        [FromQuery] DateTime? deliveryDateStart = null,
        [FromQuery] DateTime? deliveryDateEnd = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        WorkOrderQueryParams query = new()
        {
            PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy, IsDescending = isDescending,
            SalesOrderNo = salesOrderNo, ProductionMainNo = productionMainNo, ProductionSubNo = productionSubNo,
            Status = status, Specification = specification,
            DeliveryDateStart = deliveryDateStart, DeliveryDateEnd = deliveryDateEnd,
            Salesman = salesman, EndCustomer = endCustomer, PlantGrade = plantGrade
        };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }
        var result = await _workOrderService.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<WorkOrderListItemDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("list-with-plans")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkOrderListDto>>>> GetListWithPlans(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? salesOrderNo = null,
        [FromQuery] string? productionMainNo = null,
        [FromQuery] string? productionSubNo = null,
        [FromQuery] int? status = null,
        [FromQuery] string? specification = null,
        [FromQuery] string? salesman = null,
        [FromQuery] string? endCustomer = null,
        [FromQuery] string? plantGrade = null,
        [FromQuery] DateTime? deliveryDateStart = null,
        [FromQuery] DateTime? deliveryDateEnd = null,
        [FromQuery] string? planTypeFilter = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        WorkOrderQueryParams query = new()
        {
            PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy, IsDescending = isDescending,
            SalesOrderNo = salesOrderNo, ProductionMainNo = productionMainNo, ProductionSubNo = productionSubNo,
            Status = status, Specification = specification,
            DeliveryDateStart = deliveryDateStart, DeliveryDateEnd = deliveryDateEnd,
            Salesman = salesman, EndCustomer = endCustomer, PlantGrade = plantGrade,
            PlanTypeFilter = planTypeFilter
        };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }
        var result = await _workOrderService.GetPagedWithPlansAsync(query);
        return Ok(ApiResponse<PagedResult<WorkOrderListDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<WorkOrderDetailDto>>> GetById(int id)
    {
        var result = await _workOrderService.GetByIdAsync(id);
        return Ok(ApiResponse<WorkOrderDetailDto>.Ok(result, "查询成功"));
    }

    [HttpGet("by-workorder-no/{workOrderNo}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<WorkOrderDetailDto>>> GetByWorkOrderNo(string workOrderNo)
    {
        if (string.IsNullOrWhiteSpace(workOrderNo))
            return BadRequest(ApiResponse<WorkOrderDetailDto>.Fail("工单号不能为空"));
        var result = await _workOrderService.GetByWorkOrderNoAsync(workOrderNo);
        return Ok(ApiResponse<WorkOrderDetailDto>.Ok(result, "查询成功"));
    }

    [HttpGet("by-order/{salesOrderNo}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<WorkOrderListItemDto>>>> GetBySalesOrderNo(string salesOrderNo)
    {
        if (string.IsNullOrWhiteSpace(salesOrderNo))
            return BadRequest(ApiResponse<List<WorkOrderListItemDto>>.Fail("订单号不能为空"));

        var result = await _workOrderService.GetBySalesOrderNoAsync(salesOrderNo);
        return Ok(ApiResponse<List<WorkOrderListItemDto>>.Ok(result, "查询成功"));
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<UpdateWorkOrderStatusResponseDto>>> UpdateStatus(
        int id, [FromBody] UpdateWorkOrderStatusRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<UpdateWorkOrderStatusResponseDto>.Fail("请求参数无效"));

        var result = await _workOrderService.UpdateStatusAsync(id, request);
        return Ok(ApiResponse<UpdateWorkOrderStatusResponseDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _workOrderService.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpPost("{id}/soft-delete")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> SoftDelete(int id)
    {
        await _workOrderService.SoftDeleteAsync(id);
        return Ok(ApiResponse.Ok("工单已删除"));
    }

    [HttpGet("{id}/print")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintWorkOrder(int id)
    {
        var bytes = await _workOrderService.PrintWorkOrderAsync(id);
        var base64 = Convert.ToBase64String(bytes);
        return Ok(ApiResponse<string>.Ok(base64, "生成成功"));
    }

    [HttpGet("order-print")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintWorkOrdersByOrder(
        [FromQuery] string salesOrderNo)
    {
        if (string.IsNullOrWhiteSpace(salesOrderNo))
            return BadRequest(ApiResponse<string>.Fail("订单号不能为空"));

        var bytes = await _workOrderService.PrintWorkOrdersByOrderAsync(salesOrderNo);
        var base64 = Convert.ToBase64String(bytes);
        return Ok(ApiResponse<string>.Ok(base64, "生成成功"));
    }

    [HttpPost("order-print-batch")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintWorkOrdersByOrderBatch(
        [FromBody] string[] salesOrderNos)
    {
        if (salesOrderNos == null || salesOrderNos.Length == 0)
            return BadRequest(ApiResponse<string>.Fail("请选择要打印的订单"));

        var bytes = await _workOrderService.PrintWorkOrdersByOrderBatchAsync(salesOrderNos);
        var base64 = Convert.ToBase64String(bytes);
        return Ok(ApiResponse<string>.Ok(base64, "生成成功"));
    }

    [HttpPost("order-print-all")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintWorkOrdersByOrderAll(
        [FromBody] WorkOrderQueryParams query)
    {
        var bytes = await _workOrderService.PrintWorkOrdersByOrderAllAsync(query);
        var base64 = Convert.ToBase64String(bytes);
        return Ok(ApiResponse<string>.Ok(base64, "生成成功"));
    }

    #endregion

    #region 定时任务接口

    [HttpPost("check-order-change/{salesOrderId}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse>> CheckOrderChange(int salesOrderId)
    {
        await _workOrderService.CheckAndUpdateWorkOrderStatusAsync(salesOrderId);
        return Ok(ApiResponse.Ok("检测完成"));
    }

    [HttpPost("check-all-order-change")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> CheckAllOrderChange()
    {
        await _workOrderService.CheckAllOrdersChangeAsync();
        return Ok(ApiResponse.Ok("全部检测完成"));
    }

    [HttpPost("refresh-material-plan-readmodel")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> RefreshMaterialPlanReadModel()
    {
        await _workOrderService.RefreshMaterialPlanReadModelAsync();
        return Ok(ApiResponse.Ok("用料计划总览读模型刷新完成"));
    }

    // ========== 筛选上下文 ==========

    [HttpGet("filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetWorkOrderFilterContexts()
    {
        var result = await _workOrderService.GetWorkOrderFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    [HttpGet("list-all")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<WorkOrderListDto>>>> GetAllList()
    {
        var result = await _workOrderService.GetAllListAsync();
        return Ok(ApiResponse<List<WorkOrderListDto>>.Ok(result, "查询成功"));
    }

    #endregion

    #region 工单-订单关系

    [HttpGet("order-relation/{salesOrderNo}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<OrderWorkOrderRelationDto>>> GetOrderWorkOrderRelation(
        string salesOrderNo)
    {
        if (string.IsNullOrWhiteSpace(salesOrderNo))
            return BadRequest(ApiResponse<OrderWorkOrderRelationDto>.Fail("订单号不能为空"));

        var result = await _workOrderService.GetOrderWorkOrderRelationAsync(salesOrderNo);
        return Ok(ApiResponse<OrderWorkOrderRelationDto>.Ok(result, "查询成功"));
    }

    #endregion
}
