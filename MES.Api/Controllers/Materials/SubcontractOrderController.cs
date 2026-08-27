using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.Interfaces.Materials;

namespace MES.Api.Controllers.Materials;

[ApiController]
[Route("api/subcontract")]
[Authorize]
public class SubcontractOrderController : ControllerBase
{
    private readonly ISubcontractOrderService _service;
    private readonly ILogger<SubcontractOrderController> _logger;

    public SubcontractOrderController(ISubcontractOrderService service, ILogger<SubcontractOrderController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<PagedResult<SubcontractOrderDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new SubcontractQueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy,
            IsDescending = isDescending,
            Status = status,
            DateFrom = dateFrom,
            DateTo = dateTo
        };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<SubcontractOrderDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<List<SubcontractOrderDto>>>> GetAllList()
    {
        var result = await _service.GetAllListAsync();
        return Ok(ApiResponse<List<SubcontractOrderDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<SubcontractOrderDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<SubcontractOrderDto>.Ok(result, "查询成功"));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Policies.MaterialEdit)]
    public async Task<ActionResult<ApiResponse<SubcontractOrderDto>>> Create([FromBody] CreateSubcontractOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SubcontractOrderDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<SubcontractOrderDto>.Ok(result, "创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.MaterialEdit)]
    public async Task<ActionResult<ApiResponse<SubcontractOrderDto>>> Update(int id, [FromBody] UpdateSubcontractOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SubcontractOrderDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<SubcontractOrderDto>.Ok(result, "更新成功"));
    }

    [HttpPost("sync-all")]
    [Authorize(Roles = Roles.Policies.MaterialEdit)]
    public async Task<ActionResult<ApiResponse>> SyncAll()
    {
        await _service.SyncAllAsync();
        return Ok(ApiResponse.Ok("同步完成"));
    }

    [HttpPost("{id}/sync")]
    [Authorize(Roles = Roles.Policies.MaterialEdit)]
    public async Task<ActionResult<ApiResponse>> SyncSingle(int id)
    {
        await _service.SyncSingleAsync(id);
        return Ok(ApiResponse.Ok("同步完成"));
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = Roles.Policies.MaterialEdit)]
    public async Task<ActionResult<ApiResponse>> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse.Fail("请求参数无效"));
        await _service.UpdateStatusAsync(id, request);
        return Ok(ApiResponse.Ok("状态更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.MaterialDelete)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpGet("procurement-status")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<List<ProcurementStatusDto>>>> GetProcurementStatus()
    {
        var result = await _service.GetProcurementStatusAsync();
        return Ok(ApiResponse<List<ProcurementStatusDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result, "查询成功"));
    }

    // ========== 子项执行查询 ==========

    [HttpGet("return-items/list")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<PagedResult<SubcontractReturnItemListDto>>>> GetReturnItemList(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? status = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "Id" : sortBy,
            IsDescending = isDescending
        };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }
        var result = await _service.GetReturnItemListAsync(query, status);
        return Ok(ApiResponse<PagedResult<SubcontractReturnItemListDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("return-items/filter-contexts")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetReturnItemFilterContexts()
    {
        var result = await _service.GetReturnItemFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result, "查询成功"));
    }

    [HttpGet("piercing-pending")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<List<SubcontractPiercingPendingDto>>>> GetPiercingPending()
    {
        var result = await _service.GetPiercingPendingAsync();
        return Ok(ApiResponse<List<SubcontractPiercingPendingDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("piercing-in-progress")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<SubcontractPiercingInProgressResultDto>>> GetPiercingInProgress()
    {
        var result = await _service.GetPiercingInProgressAsync();
        return Ok(ApiResponse<SubcontractPiercingInProgressResultDto>.Ok(result, "查询成功"));
    }

    [HttpGet("piercing-monthly")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<SubcontractPiercingMonthlyResultDto>>> GetPiercingMonthly()
    {
        var result = await _service.GetPiercingMonthlyAsync();
        return Ok(ApiResponse<SubcontractPiercingMonthlyResultDto>.Ok(result, "查询成功"));
    }

    [HttpPost("return-items/print-selected-file")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<IActionResult> PrintReturnItemSelectedFile([FromBody] OrderPrintBatchRequest request)
    {
        try
        {
            var pdfBytes = await _service.PrintReturnItemSelectedAsync(request.Ids, request.Columns);
            return File(pdfBytes, "application/pdf", $"子项查询_选中.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打印选中子项失败");
            return StatusCode(500, ApiResponse<string>.Fail($"打印失败: {ex.Message}"));
        }
    }

    [HttpGet("mismatched-orders")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<List<OrderMismatchInfo>>>> GetMismatchedOrders()
    {
        var result = await _service.GetMismatchedSubcontractOrdersAsync();
        return Ok(ApiResponse<List<OrderMismatchInfo>>.Ok(result, "查询成功"));
    }

    // ========== 打印 ==========

    [HttpPost("{id}/print-file")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<IActionResult> PrintOrderFile(int id)
    {
        var pdfBytes = await _service.PrintOrderAsync(id);
        return File(pdfBytes, "application/pdf", $"委外单_{id}.pdf");
    }

    [HttpPost("print-batch-file")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<IActionResult> PrintOrderBatchFile([FromBody] OrderPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintOrderBatchAsync(request.Ids);
        return File(pdfBytes, "application/pdf", $"委外单批量.pdf");
    }

    [HttpPost("print-list-file")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<IActionResult> PrintListFile([FromBody] SubcontractOrderPrintListRequest request)
    {
        var pdfBytes = await _service.PrintSubcontractOrderListAsync(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "圆棒穿孔列表.pdf");
    }

    [HttpPost("print-all-file")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<IActionResult> PrintOrderAllFile([FromBody] OrderPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintOrderAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.DateFrom, request.DateTo);
        return File(pdfBytes, "application/pdf", $"委外单全部.pdf");
    }

    [HttpGet("plan-detail")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<PlanDetailDto>>> GetPlanDetail(
        [FromQuery] string workOrderNo, [FromQuery] string materialCategory)
    {
        var result = await _service.GetPlanDetailAsync(workOrderNo, materialCategory);
        if (result == null)
            return Ok(ApiResponse<PlanDetailDto>.Fail("未找到对应的用料计划"));
        return Ok(ApiResponse<PlanDetailDto>.Ok(result, "查询成功"));
    }
}
