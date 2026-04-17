// 文件路径: MES.Api/Controllers/OrderController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces.Order;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

/// <summary>
/// 订单控制器
/// </summary>
[ApiController]
[Route("api/order")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    #region 订单管理

    /// <summary>
    /// 分页查询订单列表
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<SalesOrderListDto>>>> GetPaged([FromQuery] QueryParams query)
    {
        var result = await _orderService.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<SalesOrderListDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 根据ID获取订单详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SalesOrderDetailDto>>> GetById(int id)
    {
        var result = await _orderService.GetByIdAsync(id);
        return Ok(ApiResponse<SalesOrderDetailDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建订单
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SalesOrderListDto>>> Create([FromBody] CreateSalesOrderRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<SalesOrderListDto>.Fail("请求参数无效"));
        }

        var result = await _orderService.CreateAsync(request);
        return Ok(ApiResponse<SalesOrderListDto>.Ok(result, "创建成功"));
    }

    /// <summary>
    /// 更新订单
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SalesOrderListDto>>> Update(int id, [FromBody] UpdateSalesOrderRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<SalesOrderListDto>.Fail("请求参数无效"));
        }

        var result = await _orderService.UpdateAsync(id, request);
        return Ok(ApiResponse<SalesOrderListDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除订单（软删除）
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _orderService.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    #endregion

    #region 项次管理

    /// <summary>
    /// 添加订单项次
    /// </summary>
    [HttpPost("{id}/items")]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<OrderItemDto>>> AddItem(int id, [FromBody] AddOrderItemRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<OrderItemDto>.Fail("请求参数无效"));
        }

        var result = await _orderService.AddItemAsync(id, request);
        return Ok(ApiResponse<OrderItemDto>.Ok(result, "添加成功"));
    }

    /// <summary>
    /// 更新订单项次
    /// </summary>
    [HttpPut("{id}/items/{itemId}")]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<OrderItemDto>>> UpdateItem(int id, int itemId, [FromBody] UpdateOrderItemRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<OrderItemDto>.Fail("请求参数无效"));
        }

        var result = await _orderService.UpdateItemAsync(id, itemId, request);
        return Ok(ApiResponse<OrderItemDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除订单项次（软删除）
    /// </summary>
    [HttpDelete("{id}/items/{itemId}")]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> DeleteItem(int id, int itemId)
    {
        await _orderService.DeleteItemAsync(id, itemId);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    #endregion
}