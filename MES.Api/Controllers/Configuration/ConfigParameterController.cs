using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.Configuration;
using System.Text.Json;

namespace MES.Api.Controllers.Configuration;

[ApiController]
[Route("api/config-parameter")]
[Authorize]
public class ConfigParameterController : ControllerBase
{
    private readonly IConfigParameterService _service;

    public ConfigParameterController(IConfigParameterService service)
    {
        _service = service;
    }

    [HttpGet("config-map")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, decimal>>>> GetConfigMap([FromQuery] string category)
    {
        var result = await _service.GetConfigMapAsync(category);
        return Ok(ApiResponse<Dictionary<string, decimal>>.Ok(result));
    }

    [HttpGet("filter-contexts")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<PagedResult<ConfigParameterDto>>>> GetPaged(
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
        return Ok(ApiResponse<PagedResult<ConfigParameterDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ConfigParameterDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<ConfigParameterDto>.Fail("参数不存在"));
        return Ok(ApiResponse<ConfigParameterDto>.Ok(result));
    }

    [HttpPost("save")]
    public async Task<ActionResult<ApiResponse<bool>>> Save([FromBody] ConfigParameterDto dto)
    {
        var result = await _service.SaveAsync(dto);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpPost("delete/{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        return Ok(ApiResponse<bool>.Ok(result));
    }
}
