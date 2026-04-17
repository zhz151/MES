using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Api.Controllers;

/// <summary>
/// 璁㈠崟鎺у埗鍣?/// </summary>
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
    /// 鍒嗛〉鏌ヨ璁㈠崟鍒楄〃
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = "OrderStaff,OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<PagedResult<SalesOrderListDto>>>> GetPaged([FromQuery] QueryParams query)
    {
        var result = await _orderService.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<SalesOrderListDto>>.Ok(result, "鏌ヨ鎴愬姛"));
    }

    /// <summary>
    /// 鏍规嵁ID鑾峰彇璁㈠崟璇︽儏
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "OrderStaff,OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<SalesOrderDetailDto>>> GetById(int id)
    {
        var result = await _orderService.GetByIdAsync(id);
        return Ok(ApiResponse<SalesOrderDetailDto>.Ok(result, "鏌ヨ鎴愬姛"));
    }

    /// <summary>
    /// 鍒涘缓璁㈠崟
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<SalesOrderListDto>>> Create([FromBody] CreateSalesOrderRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<SalesOrderListDto>.Fail("璇锋眰鍙傛暟鏃犳晥"));
        }

        var result = await _orderService.CreateAsync(request);
        return Ok(ApiResponse<SalesOrderListDto>.Ok(result, "鍒涘缓鎴愬姛"));
    }

    /// <summary>
    /// 鏇存柊璁㈠崟
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<SalesOrderListDto>>> Update(int id, [FromBody] UpdateSalesOrderRequest request)
    {
        var result = await _orderService.UpdateAsync(id, request);
        return Ok(ApiResponse<SalesOrderListDto>.Ok(result, "鏇存柊鎴愬姛"));
    }

    /// <summary>
    /// 鍒犻櫎璁㈠崟锛堣蒋鍒犻櫎锛?    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        await _orderService.DeleteAsync(id);
        return Ok(ApiResponse.Ok("鍒犻櫎鎴愬姛"));
    }
}