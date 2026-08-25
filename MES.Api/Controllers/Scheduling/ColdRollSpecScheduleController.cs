using MES.Core.DTOs.Scheduling;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Models;
using MES.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MES.Api.Controllers.Scheduling;

/// <summary>
/// 冷轧排程 — 冷轧看板上的排程操作入口（全量同步模式）
/// </summary>
[ApiController]
[Route("api/cold-roll-spec-schedule")]
[Authorize]
public class ColdRollSpecScheduleController : ControllerBase
{
    private readonly IColdRollSpecScheduleService _service;

    public ColdRollSpecScheduleController(IColdRollSpecScheduleService service)
    {
        _service = service;
    }

    /// <summary>获取全部排程记录</summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.SchedulingView)]
    public async Task<IActionResult> GetAll()
    {
        var data = await _service.GetAllAsync();
        return Ok(ApiResponse<List<ColdRollSpecScheduleDto>>.Ok(data));
    }

    /// <summary>全量保存排程记录（增/改 + 删除不存在的维度）</summary>
    [HttpPost("save-all")]
    [Authorize(Roles = Roles.Policies.SchedulingEdit)]
    public async Task<IActionResult> SaveAll([FromBody] List<ColdRollSpecScheduleDto> dtos)
    {
        await _service.SaveAllAsync(dtos);
        return Ok(ApiResponse.Ok("保存成功"));
    }
}
