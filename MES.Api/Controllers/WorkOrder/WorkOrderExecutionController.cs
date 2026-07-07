using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.WorkOrder;

[ApiController]
[Route("api/workorder-execution")]
[Authorize]
public class WorkOrderExecutionController : ControllerBase
{
    private readonly IWorkOrderExecutionService _service;
    private readonly IWorkOrderListSummaryRefreshService _listSummaryService;

    public WorkOrderExecutionController(IWorkOrderExecutionService service,
        IWorkOrderListSummaryRefreshService listSummaryService)
    {
        _service = service;
        _listSummaryService = listSummaryService;
    }

    /// <summary>
    /// 分页查询工单执行状况列表
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkOrderExecutionSummaryDto>>>> GetPaged(
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
        {
            try
            {
                var f = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (f != null && f.Count > 0) query.Filters = f;
            }
            catch { }
        }
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<WorkOrderExecutionSummaryDto>>.Ok(result));
    }

    /// <summary>
    /// 全量刷新所有工单的执行状况汇总
    /// </summary>
    [HttpPost("refresh-all")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<WorkOrderExecutionRefreshResultDto>>> RefreshAll()
    {
        var result = await _service.RefreshAllAsync();
        return Ok(ApiResponse<WorkOrderExecutionRefreshResultDto>.Ok(result, $"刷新完成，共{result.RefreshedCount}条"));
    }

    /// <summary>
    /// 全量刷新用料计划总览读模型
    /// </summary>
    [HttpPost("refresh-list-summary")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> RefreshListSummary()
    {
        await _listSummaryService.RefreshAllAsync();
        return Ok(ApiResponse.Ok("用料计划总览读模型刷新完成"));
    }

    /// <summary>
    /// 获取工单执行看板聚合数据
    /// </summary>
    [HttpGet("dashboard-summary")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<WorkOrderExecutionDashboardItem>>>> GetDashboardSummary()
    {
        var result = await _service.GetDashboardSummaryAsync();
        return Ok(ApiResponse<List<WorkOrderExecutionDashboardItem>>.Ok(result));
    }

    /// <summary>
    /// 获取筛选上下文（各列的筛选项列表）
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }
}
