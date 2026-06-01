using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Interfaces;
using MES.Core.Models;
using System.Text.Json;

namespace MES.Api.Controllers.Scheduling;

[ApiController]
[Route("api/sales-urging")]
[Authorize]
public class SalesUrgingController : ControllerBase
{
    private readonly ISalesUrgingService _service;

    public SalesUrgingController(ISalesUrgingService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<PagedResult<Core.DTOs.SalesUrgingDto>>>> GetPaged(
        [FromQuery] QueryParams query,
        [FromQuery] string? filters = null)
    {
        if (!string.IsNullOrEmpty(filters))
            query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<Core.DTOs.SalesUrgingDto>>.Ok(result));
    }

    [HttpPost("save")]
    public async Task<ActionResult<ApiResponse<bool>>> SaveUrging(
        [FromBody] SaveUrgingRequest request)
    {
        var result = await _service.SaveUrgingAsync(request.WorkOrderId, request.IsSalesUrging, request.UrgingRemark);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpPost("save-lock-confirmation")]
    public async Task<ActionResult<ApiResponse<bool>>> SaveLockConfirmation(
        [FromBody] SaveLockConfirmationRequest request)
    {
        var result = await _service.SaveLockConfirmationAsync(request.WorkOrderId, request.EstimatedArrivalDate, request.IsMainNoMaterialComplete);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpPost("unlock")]
    public async Task<ActionResult<ApiResponse<bool>>> Unlock([FromBody] UnlockRequest request)
    {
        var result = await _service.UnlockAsync(request.WorkOrderId);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpGet("filter-contexts")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }
}

public class SaveUrgingRequest
{
    public int WorkOrderId { get; set; }
    public bool IsSalesUrging { get; set; }
    public string? UrgingRemark { get; set; }
}

public class SaveLockConfirmationRequest
{
    public int WorkOrderId { get; set; }
    public DateTime? EstimatedArrivalDate { get; set; }
    public bool IsMainNoMaterialComplete { get; set; }
}

public class UnlockRequest
{
    public int WorkOrderId { get; set; }
}
