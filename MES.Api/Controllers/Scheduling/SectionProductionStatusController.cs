using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Scheduling;
using MES.Core.Interfaces.Scheduling;
using MES.Shared.Constants;

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
    [Authorize(Roles = Roles.Policies.SchedulingView)]
    public async Task<ActionResult<ApiResponse<List<SectionProductionStatusDto>>>> GetStatus()
    {
        var result = await _service.GetStatusAsync();
        return Ok(ApiResponse<List<SectionProductionStatusDto>>.Ok(result));
    }

    [HttpPost("print-file")]
    [Authorize(Roles = Roles.Policies.SchedulingView)]
    public async Task<IActionResult> PrintFile([FromBody] SectionProductionStatusPrintRequest request)
    {
        var pdfBytes = await _service.PrintFileAsync(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "工段待在产量.pdf");
    }
}
