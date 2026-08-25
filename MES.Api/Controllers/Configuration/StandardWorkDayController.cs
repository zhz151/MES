using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.Configuration;
using MES.Shared.Constants;
using System.Text.Json;

namespace MES.Api.Controllers.Configuration;

[ApiController]
[Route("api/standard-work-day")]
[Authorize]
public class StandardWorkDayController : ControllerBase
{
    private readonly IStandardWorkDayService _service;
    private readonly ISectionNameDisplayService _displayService;

    public StandardWorkDayController(IStandardWorkDayService service, ISectionNameDisplayService displayService)
    {
        _service = service;
        _displayService = displayService;
    }

    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.ConfigurationView)]
    public async Task<ActionResult<ApiResponse<PagedResult<StandardWorkDayDto>>>> GetPaged(
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
        return Ok(ApiResponse<PagedResult<StandardWorkDayDto>>.Ok(result));
    }

    [HttpGet("enabled-sections")]
    public async Task<ActionResult<ApiResponse<List<SectionInfoDto>>>> GetEnabledSections()
    {
        var result = await _service.GetEnabledSectionsAsync();
        return Ok(ApiResponse<List<SectionInfoDto>>.Ok(result));
    }

    /// <summary>Key → 显示中文 映射（配置表优先，兜底 SectionDefs），前端显示层用</summary>
    [HttpGet("section-name-map")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, string>>>> GetSectionNameMap()
    {
        var result = await _displayService.GetSectionNameMapAsync();
        return Ok(ApiResponse<Dictionary<string, string>>.Ok(
            new Dictionary<string, string>(result)));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.ConfigurationView)]
    public async Task<ActionResult<ApiResponse<StandardWorkDayDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<StandardWorkDayDto>.Fail("标准工作日不存在"));
        return Ok(ApiResponse<StandardWorkDayDto>.Ok(result));
    }

    [HttpPost("save")]
    [Authorize(Roles = Roles.Policies.ConfigurationEdit)]
    public async Task<ActionResult<ApiResponse<bool>>> Save([FromBody] StandardWorkDayDto dto)
    {
        var result = await _service.SaveAsync(dto);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpPost("delete/{id}")]
    [Authorize(Roles = Roles.Policies.ConfigurationDelete)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        return Ok(ApiResponse<bool>.Ok(result));
    }
}
