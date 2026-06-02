using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

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
    public async Task<ActionResult<ApiResponse<PagedResult<StandardWorkDayDeliveryStateDto>>>> GetPaged(
        [FromQuery] QueryParams query)
    {
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<StandardWorkDayDeliveryStateDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<StandardWorkDayDeliveryStateDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<StandardWorkDayDeliveryStateDto>.Ok(result));
    }

    [HttpPost("save")]
    public async Task<ActionResult<ApiResponse<bool>>> Save([FromBody] StandardWorkDayDeliveryStateDto dto)
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
