using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Scheduling;

[ApiController]
[Route(ApiEndpoints.BatchPlanTarget)]
[Authorize]
public class BatchPlanTargetController : ControllerBase
{
    private readonly IBatchPlanTargetService _service;

    public BatchPlanTargetController(IBatchPlanTargetService service)
    {
        _service = service;
    }

    [HttpGet("all")]
    public async Task<ActionResult<ApiResponse<List<BatchPlanTargetDto>>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<BatchPlanTargetDto>>.Ok(result));
    }

    [HttpPost("save-all")]
    public async Task<ActionResult<ApiResponse<bool>>> SaveAll([FromBody] List<BatchPlanTargetDto> dtos)
    {
        var result = await _service.SaveAllAsync(dtos);
        return Ok(ApiResponse<bool>.Ok(result));
    }
}
