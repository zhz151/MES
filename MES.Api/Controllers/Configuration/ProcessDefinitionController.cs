using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.Configuration;
using MES.Shared.Constants;
using System.Text.Json;

namespace MES.Api.Controllers.Configuration;

[ApiController]
[Route("api/process-definition")]
[Authorize]
public class ProcessDefinitionController : ControllerBase
{
    private readonly IProcessDefinitionService _service;

    public ProcessDefinitionController(IProcessDefinitionService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.ConfigurationView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ProcessDefinitionDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        QueryParams query = new() { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword, SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy, IsDescending = isDescending };
        if (!string.IsNullOrEmpty(filters))
            query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<ProcessDefinitionDto>>.Ok(result));
    }

    [HttpGet("enabled-processes")]
    public async Task<ActionResult<ApiResponse<List<ProcessInfoDto>>>> GetEnabledProcesses()
    {
        var result = await _service.GetEnabledProcessesAsync();
        return Ok(ApiResponse<List<ProcessInfoDto>>.Ok(result));
    }

    /// <summary>冷轧/冷拔工序选项（仅启用的 IsEnabled=true），机型下拉/工段 Tab/机台组配置工序多选动态化用</summary>
    [HttpGet("cold-roll-options")]
    public async Task<ActionResult<ApiResponse<List<ProcessInfoDto>>>> GetColdRollOptions()
    {
        var result = await _service.GetColdRollOrDrawOptionsAsync();
        return Ok(ApiResponse<List<ProcessInfoDto>>.Ok(result));
    }

    /// <summary>Key → 显示中文 映射（配置表优先，兜底 ProcessNames），前端显示层用</summary>
    [HttpGet("process-name-map")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, string>>>> GetProcessNameMap()
    {
        var result = await _service.GetProcessNameMapAsync();
        return Ok(ApiResponse<Dictionary<string, string>>.Ok(
            new Dictionary<string, string>(result)));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.ConfigurationView)]
    public async Task<ActionResult<ApiResponse<ProcessDefinitionDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<ProcessDefinitionDto>.Fail("工序组定义不存在"));
        return Ok(ApiResponse<ProcessDefinitionDto>.Ok(result));
    }

    [HttpPost("save")]
    [Authorize(Roles = Roles.Policies.ConfigurationEdit)]
    public async Task<ActionResult<ApiResponse<bool>>> Save([FromBody] ProcessDefinitionDto dto)
    {
        var result = await _service.SaveAsync(dto);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpPost("delete/{id}")]
    [Authorize(Roles = Roles.Policies.ConfigurationEdit)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        return Ok(ApiResponse<bool>.Ok(result));
    }
}
