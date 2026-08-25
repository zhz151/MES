using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Scheduling;
using MES.Core.Interfaces.Scheduling;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Scheduling;

[ApiController]
[Route("api/batch-plan-schedule")]
[Authorize]
public class BatchPlanScheduleController : ControllerBase
{
    private readonly IBatchPlanScheduleService _service;

    public BatchPlanScheduleController(IBatchPlanScheduleService service)
    {
        _service = service;
    }

    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.SchedulingView)]
    public async Task<ActionResult<ApiResponse<List<BatchPlanScheduleDto>>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<BatchPlanScheduleDto>>.Ok(result));
    }

    [HttpPost("save")]
    [Authorize(Roles = Roles.Policies.SchedulingEdit)]
    public async Task<ActionResult<ApiResponse<bool>>> Save([FromBody] BatchPlanScheduleDto dto)
    {
        var result = await _service.SaveAsync(dto);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpPost("plan-all")]
    [Authorize(Roles = Roles.Policies.SchedulingEdit)]
    public async Task<ActionResult<ApiResponse<bool>>> PlanAll([FromQuery] string? sectionTab = null)
    {
        var result = await _service.PlanAllAsync(sectionTab);
        return Ok(ApiResponse<bool>.Ok(result));
    }
}
