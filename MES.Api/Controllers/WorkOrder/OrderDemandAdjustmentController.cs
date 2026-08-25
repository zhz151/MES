using MES.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Interfaces.WorkOrder;
using System.Text.Json;

namespace MES.Api.Controllers.WorkOrder;

[ApiController]
[Route("api/order-demand-adjustment")]
[Authorize(Roles = Roles.Policies.WorkOrderView)]
public class OrderDemandAdjustmentController : ControllerBase
{
    private readonly IOrderDemandAdjustmentService _service;

    public OrderDemandAdjustmentController(IOrderDemandAdjustmentService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<PagedResult<OrderDemandAdjustmentDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] DateTime? signDateFrom = null,
        [FromQuery] DateTime? signDateTo = null,
        [FromQuery] DateTime? deliveryDateStart = null,
        [FromQuery] DateTime? deliveryDateEnd = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        QueryParams query = new() { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword, SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy, IsDescending = isDescending };
        if (!string.IsNullOrEmpty(filters))
            query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var result = await _service.GetPagedAsync(query, signDateFrom, signDateTo, deliveryDateStart, deliveryDateEnd);
        return Ok(ApiResponse<PagedResult<OrderDemandAdjustmentDto>>.Ok(result));
    }

    [HttpPost("save")]
    [Authorize(Roles = Roles.Policies.WorkOrderEdit)]
    public async Task<ActionResult<ApiResponse<bool>>> SaveUrging(
        [FromBody] SaveUrgingRequest request)
    {
        var result = await _service.SaveUrgingAsync(request.WorkOrderId, request.IsUrging, request.IsBatchDelivery, request.IsPaused, request.IsForceCompleted, request.AdjustmentRemark);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpGet("filter-contexts")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    [HttpPost("print-file")]
    public async Task<IActionResult> PrintFile([FromBody] OrderDemandAdjustmentPrintRequest request)
    {
        var pdfBytes = await _service.PrintFileAsync(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "工单需求调整.pdf");
    }

    [HttpPost("print-all-file")]
    public async Task<IActionResult> PrintAllFile([FromBody] DemandAdjustmentPrintAllRequest request)
    {
        var pdfBytes = await _service.PrintAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.SignDateFrom, request.SignDateTo, request.DeliveryDateStart, request.DeliveryDateEnd, request.Columns);
        return File(pdfBytes, "application/pdf", "工单需求调整-全部.pdf");
    }
}
