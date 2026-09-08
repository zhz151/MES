using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs.Order;
using MES.Core.Interfaces.Order;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Order;

/// <summary>
/// 订单进度树（订单号 → 主号 → 四阶段分支 → 重量叶）
/// </summary>
[ApiController]
[Route("api/order/progress")]
[Authorize]
public class OrderProgressController : ControllerBase
{
    private readonly IOrderProgressQueryService _service;

    public OrderProgressController(IOrderProgressQueryService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取指定订单号的进度树（无该订单工单时返回空 data）
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Roles.Policies.OrderView)]
    public async Task<ActionResult<ApiResponse<OrderProgressTreeDto?>>> GetProgress([FromQuery] string salesOrderNo)
    {
        if (string.IsNullOrWhiteSpace(salesOrderNo))
            return Ok(ApiResponse<OrderProgressTreeDto?>.Ok(null, "订单号为空"));

        var result = await _service.GetTreeAsync(salesOrderNo);
        return Ok(ApiResponse<OrderProgressTreeDto?>.Ok(result));
    }
}
