using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.Configuration;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Configuration;

[ApiController]
[Route("api/combination-group")]
[Authorize]
public class CombinationGroupController : ControllerBase
{
    private readonly ICombinationGroupService _service;

    public CombinationGroupController(ICombinationGroupService service)
    {
        _service = service;
    }

    /// <summary>获取全部组合归类</summary>
    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<List<CombinationGroupDto>>>> GetList()
    {
        var result = await _service.GetListAsync();
        return Ok(ApiResponse<List<CombinationGroupDto>>.Ok(result));
    }

    /// <summary>新增或更新组合归类</summary>
    [HttpPost("save")]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse>> Save([FromBody] CombinationGroupDto dto)
    {
        var success = await _service.SaveAsync(dto);
        if (!success)
            return NotFound(ApiResponse.Fail("组合归类不存在"));
        return Ok(ApiResponse.Ok("保存成功"));
    }

    /// <summary>删除组合归类</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);
        if (!success)
            return NotFound(ApiResponse.Fail("组合归类不存在"));
        return Ok(ApiResponse.Ok("删除成功"));
    }
}
