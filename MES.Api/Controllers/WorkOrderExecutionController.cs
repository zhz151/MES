using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Api.Controllers;

[ApiController]
[Route("api/workorder-execution")]
[Authorize]
public class WorkOrderExecutionController : ControllerBase
{
    private readonly IWorkOrderExecutionService _service;

    public WorkOrderExecutionController(IWorkOrderExecutionService service)
    {
        _service = service;
    }

    /// <summary>
    /// 分页查询工单执行状况列表
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetPaged([FromQuery] QueryParams query)
    {
        var result = await _service.GetPagedAsync(query);
        return Ok(Core.Models.ApiResponse<PagedResult<WorkOrderExecutionSummaryDto>>.Ok(result));
    }

    /// <summary>
    /// 全量刷新所有工单的执行状况汇总
    /// </summary>
    [HttpPost("refresh-all")]
    public async Task<IActionResult> RefreshAll()
    {
        var result = await _service.RefreshAllAsync();
        return Ok(Core.Models.ApiResponse<WorkOrderExecutionRefreshResultDto>.Ok(result, $"刷新完成，共{result.RefreshedCount}条"));
    }
}
