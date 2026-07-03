using MES.Core.DTOs;
using MES.Services.Printing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Interfaces;
using MES.Core.Models;
using System.Text.Json;

namespace MES.Api.Controllers.Orders;

[ApiController]
[Route("api/order-demand-adjustment")]
[Authorize]
public class OrderDemandAdjustmentController : ControllerBase
{
    private readonly IOrderDemandAdjustmentService _service;

    public OrderDemandAdjustmentController(IOrderDemandAdjustmentService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<PagedResult<Core.DTOs.OrderDemandAdjustmentDto>>>> GetPaged(
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
        return Ok(ApiResponse<PagedResult<Core.DTOs.OrderDemandAdjustmentDto>>.Ok(result));
    }

    [HttpPost("save")]
    public async Task<ActionResult<ApiResponse<bool>>> SaveUrging(
        [FromBody] SaveUrgingRequest request)
    {
        var result = await _service.SaveUrgingAsync(request.WorkOrderId, request.IsUrging, request.IsBatchDelivery, request.IsPaused, request.AdjustmentRemark);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpGet("filter-contexts")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    [HttpPost("print-file")]
    public IActionResult PrintFile([FromBody] OrderDemandAdjustmentPrintRequest request)
    {
        var pdfBytes = TablePrintHelper.GeneratePdf(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "订单需求调整.pdf");
    }
}

public class SaveUrgingRequest
{
    public int WorkOrderId { get; set; }
    public bool IsUrging { get; set; }
    public bool IsBatchDelivery { get; set; }
    public bool IsPaused { get; set; }
    public string? AdjustmentRemark { get; set; }
}
