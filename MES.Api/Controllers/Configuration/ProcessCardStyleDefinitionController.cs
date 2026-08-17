using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.Configuration;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Configuration;

/// <summary>
/// 工艺卡打印版式配置控制器：格式设置面板「打印版式」Tab 全量加载/批量保存（全局共享）。
/// </summary>
[ApiController]
[Route(ApiEndpoints.ProcessCardStyleDefinition)]
[Authorize]
public class ProcessCardStyleDefinitionController : ControllerBase
{
    private readonly IProcessCardStyleDefinitionService _service;

    public ProcessCardStyleDefinitionController(IProcessCardStyleDefinitionService service)
    {
        _service = service;
    }

    /// <summary>全量配置（格式设置面板「打印版式」Tab 加载），按 Key 升序</summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.ConfigurationRead)]
    public async Task<ActionResult<ApiResponse<List<ProcessCardStyleDefinitionDto>>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<ProcessCardStyleDefinitionDto>>.Ok(result));
    }

    /// <summary>配置映射：Key → Value（打印链路覆盖字体/字号用）</summary>
    [HttpGet("style-map")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, string>>>> GetStyleMap()
    {
        var result = await _service.GetStyleMapAsync();
        return Ok(ApiResponse<Dictionary<string, string>>.Ok(result));
    }

    /// <summary>批量新增/更新（锚点 Key），返回写入行数</summary>
    [HttpPost("save-all")]
    [Authorize(Roles = Roles.Policies.ConfigurationWrite)]
    public async Task<ActionResult<ApiResponse<int>>> SaveAll([FromBody] List<ProcessCardStyleDefinitionDto> items)
    {
        var result = await _service.SaveAllAsync(items);
        return Ok(ApiResponse<int>.Ok(result));
    }
}
