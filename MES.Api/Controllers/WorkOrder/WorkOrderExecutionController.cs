using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Interfaces.WorkOrder;

namespace MES.Api.Controllers.WorkOrder;

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
    [Authorize(Roles = Roles.Policies.WorkOrderView)]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkOrderExecutionSummaryDto>>>> GetPaged(
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
        {
            try
            {
                var f = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (f != null && f.Count > 0) query.Filters = f;
            }
            catch { }
        }
        var result = await _service.GetPagedAsync(query, signDateFrom, signDateTo, deliveryDateStart, deliveryDateEnd);
        return Ok(ApiResponse<PagedResult<WorkOrderExecutionSummaryDto>>.Ok(result));
    }

    /// <summary>
    /// 全量刷新所有工单的执行状况汇总
    /// </summary>
    [HttpPost("refresh-all")]
    [Authorize(Roles = Roles.Policies.WorkOrderEdit)]
    public async Task<ActionResult<ApiResponse<WorkOrderExecutionRefreshResultDto>>> RefreshAll()
    {
        var result = await _service.RefreshAllAsync();
        return Ok(ApiResponse<WorkOrderExecutionRefreshResultDto>.Ok(result, $"刷新完成，共{result.RefreshedCount}条"));
    }

    /// <summary>
    /// 获取工单执行看板聚合数据
    /// </summary>
    [HttpGet("dashboard-summary")]
    [Authorize(Roles = Roles.Policies.WorkOrderView)]
    public async Task<ActionResult<ApiResponse<List<WorkOrderExecutionDashboardItem>>>> GetDashboardSummary()
    {
        var result = await _service.GetDashboardSummaryAsync();
        return Ok(ApiResponse<List<WorkOrderExecutionDashboardItem>>.Ok(result));
    }

    /// <summary>
    /// 获取「错误疑问投料」明细（到料实投一致性 ∈ {2,3,4,5} 的全量工单行）
    /// </summary>
    [HttpGet("error-doubt-inputs")]
    [Authorize(Roles = Roles.Policies.WorkOrderView)]
    public async Task<ActionResult<ApiResponse<List<ErrorDoubtInputItemDto>>>> GetErrorDoubtInputItems()
    {
        var result = await _service.GetErrorDoubtInputItemsAsync();
        return Ok(ApiResponse<List<ErrorDoubtInputItemDto>>.Ok(result));
    }

    /// <summary>
    /// 获取「在产在检-错疑待料」聚合（主号-关注 = 主号完成/生产执行/成品检验 三档 × 理论原料未至/工单到料未投 的工单数+累计重量）
    /// </summary>
    [HttpGet("in-production-inspection-doubt-items")]
    [Authorize(Roles = Roles.Policies.WorkOrderView)]
    public async Task<ActionResult<ApiResponse<List<InProductionInspectionDoubtItemDto>>>> GetInProductionInspectionDoubtItems()
    {
        var result = await _service.GetInProductionInspectionDoubtItemsAsync();
        return Ok(ApiResponse<List<InProductionInspectionDoubtItemDto>>.Ok(result));
    }

    /// <summary>
    /// 获取筛选上下文（各列的筛选项列表）
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.WorkOrderView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    /// <summary>
    /// 打印选中行
    /// </summary>
    [HttpPost("print-file")]
    [Authorize(Roles = Roles.Policies.WorkOrderView)]
    public async Task<IActionResult> PrintFile([FromBody] WorkOrderExecutionPrintRequest request)
    {
        var pdfBytes = await _service.PrintFileAsync(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "工单执行状况.pdf");
    }

    /// <summary>
    /// 打印全部（按筛选条件）
    /// </summary>
    [HttpPost("print-all-file")]
    [Authorize(Roles = Roles.Policies.WorkOrderView)]
    public async Task<IActionResult> PrintAllFile([FromBody] WorkOrderExecutionPrintAllRequest request)
    {
        var pdfBytes = await _service.PrintAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.SignDateFrom, request.SignDateTo, request.DeliveryDateStart, request.DeliveryDateEnd, request.Columns);
        return File(pdfBytes, "application/pdf", "工单执行状况-全部.pdf");
    }
}
