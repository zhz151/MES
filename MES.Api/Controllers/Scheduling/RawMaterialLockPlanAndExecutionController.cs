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
[Route("api/raw-material-lock-plan")]
[Authorize]
public class RawMaterialLockPlanAndExecutionController : ControllerBase
{
    private readonly IRawMaterialLockPlanAndExecutionService _service;

    public RawMaterialLockPlanAndExecutionController(IRawMaterialLockPlanAndExecutionService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<PagedResult<RawMaterialLockPlanAndExecutionDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        QueryParams query = new() { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword, SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy, IsDescending = isDescending };
        if (!string.IsNullOrEmpty(filters))
            query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<RawMaterialLockPlanAndExecutionDto>>.Ok(result));
    }

    [HttpPost("set-pre-execute-flags")]
    public async Task<ActionResult<ApiResponse<SetPreExecuteFlagsResult>>> SetPreExecuteFlags([FromBody] SetPreExecuteFlagsRequest request)
    {
        var result = await _service.SetPreExecuteFlagsAsync(request.WorkOrderIds, request.IsPreInput, request.IsMainNoMaterialComplete, request.BudgetInputDate, request.IsBudgetComplete);
        return Ok(ApiResponse<SetPreExecuteFlagsResult>.Ok(result, result.Message));
    }

    [HttpPost("print")]
    public ActionResult<ApiResponse<string>> Print([FromBody] RawMaterialLockPlanPrintRequest request)
    {
        var pdfBytes = TablePrintHelper.GeneratePdf(request.Title, request.Items, request.Columns);
        return Ok(ApiResponse<string>.Ok(Convert.ToBase64String(pdfBytes)));
    }

    [HttpPost("print-file")]
    public IActionResult PrintFile([FromBody] RawMaterialLockPlanPrintRequest request)
    {
        var pdfBytes = TablePrintHelper.GeneratePdf(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "原锁计划.pdf");
    }
}

public class SetPreExecuteFlagsRequest
{
    public List<int> WorkOrderIds { get; set; } = new();
    public bool? IsPreInput { get; set; }
    public bool? IsMainNoMaterialComplete { get; set; }
    public DateTime? BudgetInputDate { get; set; }
    public bool? IsBudgetComplete { get; set; }
}
