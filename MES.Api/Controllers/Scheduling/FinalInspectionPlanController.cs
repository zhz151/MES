using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Scheduling;
using MES.Core.Interfaces.Scheduling;
using MES.Shared.Constants;

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
    [Authorize(Roles = Roles.Policies.SchedulingView)]
    public async Task<ActionResult<ApiResponse<List<FinalInspectionPlanDto>>>> GetKanban()
    {
        var result = await _service.GetKanbanAsync();
        return Ok(ApiResponse<List<FinalInspectionPlanDto>>.Ok(result));
    }

    [HttpGet("summary")]
    [Authorize(Roles = Roles.Policies.SchedulingView)]
    public async Task<ActionResult<ApiResponse<List<FinalInspectionPlanSummaryRowDto>>>> GetSummary()
    {
        var result = await _service.GetSummaryAsync();
        return Ok(ApiResponse<List<FinalInspectionPlanSummaryRowDto>>.Ok(result));
    }

    [HttpPost("print-file")]
    [Authorize(Roles = Roles.Policies.SchedulingView)]
    public async Task<IActionResult> PrintFile([FromBody] FinalInspectionPlanPrintRequest request)
    {
        var pdfBytes = await _service.PrintFileAsync(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "成检计划.pdf");
    }
}
