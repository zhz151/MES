using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Interfaces;
using MES.Core.Models;
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
    public async Task<ActionResult<ApiResponse<PagedResult<Core.DTOs.WorkOrderScheduleDto>>>> GetPaged(
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
        return Ok(ApiResponse<PagedResult<Core.DTOs.WorkOrderScheduleDto>>.Ok(result));
    }

    [HttpPost("plan-arrangement")]
    public async Task<ActionResult<ApiResponse<int>>> PlanArrangement()
    {
        var count = await _service.PlanArrangementAsync();
        return Ok(ApiResponse<int>.Ok(count, $"计划安排完成，共{count}条"));
    }

    [HttpPost("execute-data-update")]
    public async Task<ActionResult<ApiResponse<int>>> ExecuteDataUpdate()
    {
        var count = await _service.ExecuteDataUpdateAsync();
        return Ok(ApiResponse<int>.Ok(count, $"执行数据更新完成，共更新{count}条"));
    }

    [HttpGet("filter-contexts")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }
}
