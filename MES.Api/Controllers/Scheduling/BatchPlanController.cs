using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Scheduling;
using MES.Core.Interfaces.Scheduling;
using System.Text.Json;

namespace MES.Api.Controllers.Scheduling;

[ApiController]
[Route("api/batch-plan")]
[Authorize]
public class BatchPlanController : ControllerBase
{
    private readonly IBatchPlanService _service;

    public BatchPlanController(IBatchPlanService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<PagedResult<BatchPlanDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "BatchNo" : sortBy,
            IsDescending = isDescending
        };
        if (!string.IsNullOrEmpty(filters))
            query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<BatchPlanDto>>.Ok(result));
    }

    [HttpGet("all")]
    public async Task<ActionResult<ApiResponse<List<BatchPlanDto>>>> GetAll(
        [FromQuery] string? sectionTab = null)
    {
        var result = await _service.GetAllAsync(sectionTab);
        return Ok(ApiResponse<List<BatchPlanDto>>.Ok(result));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<List<BatchPlanSummaryRowDto>>>> GetSummary()
    {
        var result = await _service.GetSummaryAsync();
        return Ok(ApiResponse<List<BatchPlanSummaryRowDto>>.Ok(result));
    }

    [HttpGet("monthly-summary")]
    public async Task<ActionResult<ApiResponse<List<BatchPlanMonthlySummaryRowDto>>>> GetMonthlySummary()
    {
        var result = await _service.GetMonthlySummaryAsync();
        return Ok(ApiResponse<List<BatchPlanMonthlySummaryRowDto>>.Ok(result));
    }

    [HttpGet("outsource-pending")]
    public async Task<ActionResult<ApiResponse<BatchPlanOutsourcePendingDto>>> GetOutsourcePending()
    {
        var result = await _service.GetOutsourcePendingAsync();
        return Ok(ApiResponse<BatchPlanOutsourcePendingDto>.Ok(result));
    }

    [HttpGet("filter-contexts")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    [HttpPost("print-file")]
    public async Task<IActionResult> PrintFile([FromBody] BatchPlanPrintRequest request)
    {
        var pdfBytes = await _service.PrintFileAsync(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "批次计划.pdf");
    }

}
