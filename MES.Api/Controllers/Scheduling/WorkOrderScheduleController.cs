using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Services.Printing;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Interfaces.Scheduling;
using System.Text.Json;

namespace MES.Api.Controllers.Scheduling;

[ApiController]
[Route("api/workorder-schedule")]
[Authorize]
public class WorkOrderScheduleController : ControllerBase
{
    private readonly IWorkOrderScheduleService _service;

    public WorkOrderScheduleController(IWorkOrderScheduleService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkOrderScheduleDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        QueryParams query = new() { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword, SortBy = string.IsNullOrEmpty(sortBy) ? "WorkOrderNo" : sortBy, IsDescending = isDescending };
        if (!string.IsNullOrEmpty(filters))
            query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<WorkOrderScheduleDto>>.Ok(result));
    }

    [HttpGet("filter-contexts")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    [HttpPost("save-plan")]
    public async Task<ActionResult<ApiResponse<bool>>> SavePlan([FromBody] SaveWorkOrderPlanRequest request)
    {
        var result = await _service.SavePlanAsync(request);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpPost("plan-all")]
    public async Task<ActionResult<ApiResponse<bool>>> PlanAll([FromBody] QueryParams query)
    {
        var result = await _service.PlanScheduleAllAsync(query);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpPost("plan-keep-attention")]
    public async Task<ActionResult<ApiResponse<bool>>> PlanKeepAttention([FromBody] QueryParams query)
    {
        var result = await _service.PlanScheduleKeepAttentionAsync(query);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpPost("print-file")]
    public IActionResult PrintFile([FromBody] WorkOrderSchedulePrintRequest request)
    {
        var pdfBytes = WorkOrderSchedulePrintHelper.GeneratePdf(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "工单计划.pdf");
    }
}
