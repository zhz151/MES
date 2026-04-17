using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Api.Controllers;

/// <summary>
/// 订单控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// 分页查询订单列表
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = "OrderStaff,OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<PagedResult<SalesOrderListDto>>>> GetPaged([FromQuery] QueryParams query)
    {
        var result = await _orderService.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<SalesOrderListDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 根据ID获取订单详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "OrderStaff,OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<SalesOrderDetailDto>>> GetById(int id)
    {
        var result = await _orderService.GetByIdAsync(id);
        return Ok(ApiResponse<SalesOrderDetailDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建订单
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "OrderDirector,Admin")]
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
    [Authorize(Roles = "OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<SalesOrderListDto>>> Update(int id, [FromBody] UpdateSalesOrderRequest request)
    {
        var result = await _orderService.UpdateAsync(id, request);
        return Ok(ApiResponse<SalesOrderListDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除订单（软删除）
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        await _orderService.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }
}