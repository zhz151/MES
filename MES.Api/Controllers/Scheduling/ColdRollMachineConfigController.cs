using MES.Core.DTOs.Scheduling;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Models;
using MES.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MES.Api.Controllers.Scheduling;

/// <summary>
/// 冷轧机台数配置 — 机台数参数表查询与维护（排程建议引擎产能平衡输入）
/// </summary>
[ApiController]
[Route("api/cold-roll-machine-config")]
[Authorize]
public class ColdRollMachineConfigController : ControllerBase
{
    private readonly IColdRollMachineConfigService _service;

    public ColdRollMachineConfigController(IColdRollMachineConfigService service)
    {
        _service = service;
    }

    /// <summary>获取全部机台数配置</summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.ConfigurationView)]
    public async Task<IActionResult> GetAll()
    {
        var data = await _service.GetAllAsync();
        return Ok(ApiResponse<List<ColdRollMachineConfigDto>>.Ok(data));
    }

    /// <summary>分页查询机台数配置</summary>
    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.ConfigurationView)]
    public async Task<IActionResult> GetPaged([FromQuery] QueryParams query)
    {
        var data = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<ColdRollMachineConfigDto>>.Ok(data));
    }

    /// <summary>保存机台数配置（新增/更新）</summary>
    [HttpPost("save")]
    [Authorize(Roles = Roles.Policies.ConfigurationEdit)]
    public async Task<IActionResult> Save([FromBody] ColdRollMachineConfigDto dto)
    {
        await _service.SaveAsync(dto);
        return Ok(ApiResponse.Ok("保存成功"));
    }

    /// <summary>删除机台数配置</summary>
    [HttpPost("delete/{id:int}")]
    [Authorize(Roles = Roles.Policies.ConfigurationDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }
}
