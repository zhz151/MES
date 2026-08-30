using MES.Core.DTOs.Scheduling;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Models;
using MES.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MES.Api.Controllers.Scheduling;

/// <summary>
/// 冷轧机台组配置 — 冷轧工序归组参数表查询与维护（排程建议/排机估算引擎机台类型组归并输入）
/// </summary>
[ApiController]
[Route("api/cold-roll-machine-group-config")]
[Authorize]
public class ColdRollMachineGroupConfigController : ControllerBase
{
    private readonly IColdRollMachineGroupConfigService _service;

    public ColdRollMachineGroupConfigController(IColdRollMachineGroupConfigService service)
    {
        _service = service;
    }

    /// <summary>获取全部机台组配置</summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.ColdRollMachineGroupConfigView)]
    public async Task<IActionResult> GetAll()
    {
        var data = await _service.GetAllAsync();
        return Ok(ApiResponse<List<ColdRollMachineGroupConfigDto>>.Ok(data));
    }

    /// <summary>分页查询机台组配置</summary>
    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.ColdRollMachineGroupConfigView)]
    public async Task<IActionResult> GetPaged([FromQuery] QueryParams query)
    {
        var data = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<ColdRollMachineGroupConfigDto>>.Ok(data));
    }

    /// <summary>保存机台组配置（新增/更新）</summary>
    [HttpPost("save")]
    [Authorize(Roles = Roles.Policies.ColdRollMachineGroupConfigEdit)]
    public async Task<IActionResult> Save([FromBody] ColdRollMachineGroupConfigDto dto)
    {
        await _service.SaveAsync(dto);
        return Ok(ApiResponse.Ok("保存成功"));
    }

    /// <summary>删除机台组配置</summary>
    [HttpPost("delete/{id:int}")]
    [Authorize(Roles = Roles.Policies.ColdRollMachineGroupConfigDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }
}
