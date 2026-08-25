using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.Configuration;
using MES.Shared.Constants;
using System.Text.Json;

namespace MES.Api.Controllers.Configuration;

[ApiController]
[Route("api/standard-work-day-delivery-state")]
[Authorize]
public class StandardWorkDayDeliveryStateController : ControllerBase
{
    private readonly IStandardWorkDayDeliveryStateService _service;

    public StandardWorkDayDeliveryStateController(IStandardWorkDayDeliveryStateService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.ConfigurationView)]
    public async Task<ActionResult<ApiResponse<PagedResult<StandardWorkDayDeliveryStateDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword, SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy, IsDescending = isDescending };
        if (!string.IsNullOrEmpty(filters))
            query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<StandardWorkDayDeliveryStateDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.ConfigurationView)]
    public async Task<ActionResult<ApiResponse<StandardWorkDayDeliveryStateDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(ApiResponse<StandardWorkDayDeliveryStateDto>.Ok(result));
    }

    [HttpPost("save")]
    [Authorize(Roles = Roles.Policies.ConfigurationEdit)]
    public async Task<ActionResult<ApiResponse<bool>>> Save([FromBody] StandardWorkDayDeliveryStateDto dto)
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
