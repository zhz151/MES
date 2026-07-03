using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Services.Printing;

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

    [HttpPost("print-file")]
    public IActionResult PrintFile([FromBody] SectionProductionStatusPrintRequest request)
    {
        var pdfBytes = TablePrintHelper.GeneratePdf(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "工段待产量.pdf");
    }
}
