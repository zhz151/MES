using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Api.Controllers.Scheduling;

[ApiController]
[Route("api/section-production-status")]
[Authorize]
public class SectionProductionStatusController : ControllerBase
{
    private readonly ISectionProductionStatusService _service;

    public SectionProductionStatusController(ISectionProductionStatusService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<SectionProductionStatusDto>>>> GetStatus()
    {
        var result = await _service.GetStatusAsync();
        return Ok(ApiResponse<List<SectionProductionStatusDto>>.Ok(result));
    }
}
