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
        [FromQuery] WorkOrderQueryParams query)
    {
        var result = await _workOrderService.GetOrderWorkOrderStatusPageAsync(query);
        return Ok(ApiResponse<PagedResult<OrderWorkOrderStatusDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("cancelled-orders")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<CancelledOrderDto>>>> GetCancelledOrders()
    {
        var result = await _workOrderService.GetCancelledOrdersAsync();
        return Ok(ApiResponse<List<CancelledOrderDto>>.Ok(result, "查询成功"));
    }

    #endregion

    #region 工单生成

    [HttpGet("items-for-generation")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<OrderItemForWorkOrderDto>>>> GetOrderItemsForWorkOrder(
        [FromQuery] string salesOrderNo)
    {
        if (string.IsNullOrWhiteSpace(salesOrderNo))
            return BadRequest(ApiResponse<List<OrderItemForWorkOrderDto>>.Fail("订单号不能为空"));

        var result = await _workOrderService.GetOrderItemsForWorkOrderAsync(salesOrderNo);
        return Ok(ApiResponse<List<OrderItemForWorkOrderDto>>.Ok(result, "查询成功"));
    }

    [HttpPost("generate")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
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
    public async Task<ActionResult<ApiResponse<PagedResult<WorkOrderListDto>>>> GetList(
        [FromQuery] WorkOrderQueryParams query)
    {
        var result = await _workOrderService.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<WorkOrderListDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<WorkOrderDetailDto>>> GetById(int id)
    {
        var result = await _workOrderService.GetByIdAsync(id);
        return Ok(ApiResponse<WorkOrderDetailDto>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}/order-items")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<OrderItemForWorkOrderDto>>>> GetOrderItems(int id)
    {
        var result = await _workOrderService.GetWorkOrderItemsAsync(id);
        return Ok(ApiResponse<List<OrderItemForWorkOrderDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("by-order/{salesOrderNo}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<WorkOrderListDto>>>> GetBySalesOrderNo(string salesOrderNo)
    {
        if (string.IsNullOrWhiteSpace(salesOrderNo))
            return BadRequest(ApiResponse<List<WorkOrderListDto>>.Fail("订单号不能为空"));

        var result = await _workOrderService.GetBySalesOrderNoAsync(salesOrderNo);
        return Ok(ApiResponse<List<WorkOrderListDto>>.Ok(result, "查询成功"));
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
        return Ok(ApiResponse.Ok("工单已软删除"));
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
    /// <summary>
/// 获取订单的工单项次追溯关系（包含该订单下所有工单及其项次明细）
/// </summary>
[HttpGet("order-relation")]
[Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
public async Task<ActionResult<ApiResponse<OrderWorkOrderRelationDto>>> GetOrderWorkOrderRelation(
    [FromQuery] string salesOrderNo)
{
    if (string.IsNullOrWhiteSpace(salesOrderNo))
        return BadRequest(ApiResponse<OrderWorkOrderRelationDto>.Fail("订单号不能为空"));

    var result = await _workOrderService.GetOrderWorkOrderRelationAsync(salesOrderNo);
    return Ok(ApiResponse<OrderWorkOrderRelationDto>.Ok(result, "查询成功"));
}
    #endregion
}