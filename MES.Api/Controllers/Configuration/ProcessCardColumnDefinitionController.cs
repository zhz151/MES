using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.Configuration;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Configuration;

/// <summary>
/// 工艺卡打印列布局配置控制器：格式设置面板全量加载/批量保存（全局共享）。
/// </summary>
[ApiController]
[Route(ApiEndpoints.ProcessCardColumnDefinition)]
[Authorize]
public class ProcessCardColumnDefinitionController : ControllerBase
{
    private readonly IProcessCardColumnDefinitionService _service;

    public ProcessCardColumnDefinitionController(IProcessCardColumnDefinitionService service)
    {
        _service = service;
    }

    /// <summary>全量配置（格式设置面板加载），按 BlockKey 升序 + ColumnIndex 升序</summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.ConfigurationView)]
    public async Task<ActionResult<ApiResponse<List<ProcessCardColumnDefinitionDto>>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<ProcessCardColumnDefinitionDto>>.Ok(result));
    }

    /// <summary>配置映射：BlockKey|FieldKey → 配置 DTO（打印链路覆盖请求列定义用）</summary>
    [HttpGet("config-map")]
    [Authorize(Roles = Roles.Policies.ConfigurationView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, ProcessCardColumnDefinitionDto>>>> GetConfigMap()
    {
        var result = await _service.GetConfigMapAsync();
        return Ok(ApiResponse<Dictionary<string, ProcessCardColumnDefinitionDto>>.Ok(result));
    }

    /// <summary>批量新增/更新（锚点 BlockKey+FieldKey），返回写入行数</summary>
    [HttpPost("save-all")]
    [Authorize(Roles = Roles.Policies.ConfigurationEdit)]
    public async Task<ActionResult<ApiResponse<int>>> SaveAll([FromBody] List<ProcessCardColumnDefinitionDto> items)
    {
        var result = await _service.SaveAllAsync(items);
        return Ok(ApiResponse<int>.Ok(result));
    }
}
