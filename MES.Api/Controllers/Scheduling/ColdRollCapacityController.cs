using MES.Core.DTOs.Scheduling;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MES.Api.Controllers.Scheduling;

/// <summary>
/// 冷轧产能配置 — 产能档案查询与手工调整（排程保存自动反哺，本控制器提供独立写端点）
/// </summary>
[ApiController]
[Route("api/cold-roll-capacity")]
[Authorize]
public class ColdRollCapacityController : ControllerBase
{
    private readonly IColdRollCapacityService _service;

    public ColdRollCapacityController(IColdRollCapacityService service)
    {
        _service = service;
    }

    /// <summary>获取全部产能配置</summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var data = await _service.GetAllAsync();
        return Ok(ApiResponse<List<ColdRollCapacityDto>>.Ok(data));
    }

    /// <summary>分页查询产能配置</summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetPaged([FromQuery] QueryParams query)
    {
        var data = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<ColdRollCapacityDto>>.Ok(data));
    }

    /// <summary>保存产能配置（手工调整机台/日产能，反向同步排程小表已存在维度）</summary>
    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] ColdRollCapacityDto dto)
    {
        await _service.SaveAsync(dto);
        return Ok(ApiResponse.Ok("保存成功"));
    }
}
