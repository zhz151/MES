using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs.Order;
using MES.Core.Interfaces.Order;
using MES.Core.Models;

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
    /// 获取指定订单号的进度树（无该订单工单时返回空 data）。
    /// ⚠️ 仅需登录、**不带订单角色档**（2026-09-14）：首页「订单进度查询」卡对所有登录用户开放
    /// （首页只有 [Authorize]，挂 OrderView 会让无订单权限的用户一查就 403）；菜单门控仍由「订单管理」分组承担。
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponse<OrderProgressTreeDto?>>> GetProgress([FromQuery] string salesOrderNo)
    {
        if (string.IsNullOrWhiteSpace(salesOrderNo))
            return Ok(ApiResponse<OrderProgressTreeDto?>.Ok(null, "订单号为空"));

        var result = await _service.GetTreeAsync(salesOrderNo);
        return Ok(ApiResponse<OrderProgressTreeDto?>.Ok(result));
    }
}
