using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

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
}
