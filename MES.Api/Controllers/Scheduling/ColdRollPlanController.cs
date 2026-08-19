using MES.Core.DTOs.Scheduling;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Models;
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
        return Ok(ApiResponse<List<ColdRollPlanRowDto>>.Ok(data));
    }

    /// <summary>
    /// 获取冷轧排程汇总（分档与主列表统一：在轧/待轧 各分 总量/特急/急/余量）
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetScheduleSummary([FromQuery] string? sectionFilter, [FromQuery] int? maxDiff)
    {
        var data = await _coldRollPlanService.GetScheduleSummaryAsync(sectionFilter, maxDiff);
        return Ok(ApiResponse<List<ColdRollPlanSummaryDto>>.Ok(data));
    }

    /// <summary>
    /// 获取冷轧排程排机估算（按轧机类型聚合：冷轧5060/冷轧2030/冷轧三辊/冷拔）
    /// </summary>
    [HttpGet("machine-estimate")]
    public async Task<IActionResult> GetMachineEstimate()
    {
        var data = await _coldRollPlanService.GetMachineEstimateAsync();
        return Ok(ApiResponse<List<ColdRollMachineEstimateDto>>.Ok(data));
    }

}
