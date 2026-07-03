using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;
using System.Text.Json;

namespace MES.Api.Controllers.Configuration;

[ApiController]
[Route("api/daily-production-capacity")]
[Authorize]
public class DailyProductionCapacityController : ControllerBase
{
    private readonly IDailyProductionCapacityService _service;

    public DailyProductionCapacityController(IDailyProductionCapacityService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<PagedResult<DailyProductionCapacityDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy,
            IsDescending = isDescending
        };
        if (!string.IsNullOrEmpty(filters))
            query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<DailyProductionCapacityDto>>.Ok(result));
    }

    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<List<DailyProductionCapacityDto>>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<DailyProductionCapacityDto>>.Ok(result));
    }

    [HttpPost("save")]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<bool>>> Save([FromBody] DailyProductionCapacityDto dto)
    {
        var result = await _service.SaveAsync(dto);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpPost("delete/{id}")]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        return Ok(ApiResponse<bool>.Ok(result));
    }
}
