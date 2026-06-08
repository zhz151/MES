using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;

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
    public async Task<IActionResult> GetAll()
    {
        var data = await _service.GetAllAsync();
        return Ok(new { Success = true, Data = data });
    }

    /// <summary>全量保存排程记录（增/改 + 删除不存在的维度）</summary>
    [HttpPost("save-all")]
    public async Task<IActionResult> SaveAll([FromBody] List<ColdRollSpecScheduleDto> dtos)
    {
        await _service.SaveAllAsync(dtos);
        return Ok(new { Success = true });
    }
}
