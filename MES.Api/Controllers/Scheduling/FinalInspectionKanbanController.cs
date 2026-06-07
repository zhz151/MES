using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Api.Controllers.Scheduling;

[ApiController]
[Route("api/final-inspection-kanban")]
[Authorize]
public class FinalInspectionKanbanController : ControllerBase
{
    private readonly IFinalInspectionKanbanService _service;

    public FinalInspectionKanbanController(IFinalInspectionKanbanService service)
    {
        _service = service;
    }

    [HttpGet("kanban")]
    public async Task<ActionResult<ApiResponse<List<FinalInspectionKanbanDto>>>> GetKanban()
    {
        var result = await _service.GetKanbanAsync();
        return Ok(ApiResponse<List<FinalInspectionKanbanDto>>.Ok(result));
    }
}
