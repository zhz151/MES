using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Scheduling;
using MES.Core.Interfaces.Scheduling;

namespace MES.Api.Controllers.Scheduling;

[ApiController]
[Route("api/final-inspection-plan")]
[Authorize]
public class FinalInspectionPlanController : ControllerBase
{
    private readonly IFinalInspectionPlanService _service;

    public FinalInspectionPlanController(IFinalInspectionPlanService service)
    {
        _service = service;
    }

    [HttpGet("kanban")]
    public async Task<ActionResult<ApiResponse<List<FinalInspectionPlanDto>>>> GetKanban()
    {
        var result = await _service.GetKanbanAsync();
        return Ok(ApiResponse<List<FinalInspectionPlanDto>>.Ok(result));
    }

    [HttpPost("print-file")]
    public async Task<IActionResult> PrintFile([FromBody] FinalInspectionPlanPrintRequest request)
    {
        var pdfBytes = await _service.PrintFileAsync(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "成检计划.pdf");
    }
}
