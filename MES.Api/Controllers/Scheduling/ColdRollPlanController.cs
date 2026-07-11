using MES.Core.Interfaces.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MES.Api.Controllers.Scheduling;

/// <summary>
/// 冷轧计划看板 — 按规格维度聚合生产批次的时间桶重量分布
/// </summary>
[ApiController]
[Route("api/cold-roll-plan")]
[Authorize]
public class ColdRollPlanController : ControllerBase
{
    private readonly IColdRollPlanService _coldRollPlanService;

    public ColdRollPlanController(IColdRollPlanService coldRollPlanService)
    {
        _coldRollPlanService = coldRollPlanService;
    }

    /// <summary>
    /// 获取冷轧计划看板数据
    /// </summary>
    [HttpGet("plan")]
    public async Task<IActionResult> GetPlan([FromQuery] string? sectionFilter)
    {
        var data = await _coldRollPlanService.GetPlanAsync(sectionFilter);
        return Ok(new { Success = true, Data = data });
    }

}
