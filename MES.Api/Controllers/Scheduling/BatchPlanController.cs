using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Services.Printing;
using System.Text.Json;

namespace MES.Api.Controllers.Scheduling;

[ApiController]
[Route("api/batch-plan")]
[Authorize]
public class BatchPlanController : ControllerBase
{
    private readonly IBatchPlanService _service;
    private readonly IProductionRecordService _prodRecordService;

    public BatchPlanController(IBatchPlanService service, IProductionRecordService prodRecordService)
    {
        _service = service;
        _prodRecordService = prodRecordService;
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

    [HttpGet("filter-contexts")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    [HttpPost("print-file")]
    public IActionResult PrintFile([FromBody] BatchPlanPrintRequest request)
    {
        var pdfBytes = TablePrintHelper.GeneratePdf(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "批次计划.pdf");
    }

    [HttpGet("debug-iskeybatch")]
    public async Task<ActionResult> DebugIsKeyBatch()
    {
        // 先触发批次跟踪重算
        var result = await _service.GetAllAsync(null);
        var target = result.Where(x => x.WorkOrderNo == "D26Z6409003").ToList();
        foreach (var item in target)
            await _prodRecordService.RefreshBatchTrackingFieldsAsync(item.BatchId);

        // 重算后重新获取
        result = await _service.GetAllAsync(null);
        target = result.Where(x => x.WorkOrderNo == "D26Z6409003").ToList();
        var html = "<table border='1'><tr><th>WorkOrderNo</th><th>Stage</th><th>Urgency</th><th>Completed</th><th>GroupName</th><th>NextProc</th><th>MainNoAttn</th><th>IsUrging</th><th>IsBatchDel</th><th>IsKeyBatch</th><th>DebugInfo</th></tr>";
        foreach (var item in target)
        {
            html += $"<tr><td>{item.WorkOrderNo}</td><td>{item.ScheduleStage}</td><td>{item.UrgencyLevel}</td><td>{item.CurrentSectionCompleted}</td><td>{item.CurrentGroupName}</td><td>{item.NextProcess}</td><td>{item.MainNoAttentionProcess}</td><td>{item.IsUrging}</td><td>{item.IsBatchDelivery}</td><td>{item.IsKeyBatch}</td><td>{item.DebugInfo}</td></tr>";
        }
        html += "</table>";
        return Content(html, "text/html");
    }
    [HttpGet("flow-summary")]
    public async Task<ActionResult<ApiResponse<List<ColdRollScheduleSummaryDto>>>> GetFlowSummary(
        [FromQuery] string? sectionTab = null,
        [FromQuery] int? maxDiff = null)
    {
        var result = await _service.GetFlowSummaryAsync(sectionTab, maxDiff);
        return Ok(ApiResponse<List<ColdRollScheduleSummaryDto>>.Ok(result));
    }
}
