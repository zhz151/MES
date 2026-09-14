using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs.Order;
using MES.Core.Interfaces.Order;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Order;

/// <summary>
/// 投料产出总况（订单列表页卡片：按订单完成月聚合的投料 / 产出 / 退货）
/// </summary>
[ApiController]
[Route("api/order/throughput-summary")]
[Authorize]
public class OrderThroughputController : ControllerBase
{
    private readonly IOrderThroughputQueryService _service;

    public OrderThroughputController(IOrderThroughputQueryService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取投料产出总况（scope 为生产类型范围：All 全部四种 / Pure 纯生产 / 单一生产类型 Key；
    /// 空值或未知值按 All 处理）。日期区间 dateFrom / dateTo（yyyy-MM-dd）任一端有值即按
    /// 「订单真实完成日落入区间」取数并**聚合为单行**（行首列 = 所选范围文本），
    /// 两端皆空回落默认「最近 12 个月（含当月）」并按完成月分行。
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Roles.Policies.OrderView)]
    public async Task<ActionResult<ApiResponse<OrderThroughputSummaryDto>>> GetSummary(
        [FromQuery] string? scope,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null)
    {
        var result = await _service.GetMonthlySummaryAsync(scope, dateFrom, dateTo);
        return Ok(ApiResponse<OrderThroughputSummaryDto>.Ok(result));
    }
}
