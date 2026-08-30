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
[Route("api/purchase-order")]
[Authorize]
public class PurchaseOrderController : ControllerBase
{
    private readonly IPurchaseOrderService _service;

    public PurchaseOrderController(IPurchaseOrderService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<PagedResult<PurchaseOrderDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] DateTime? requiredDateFrom = null,
        [FromQuery] DateTime? requiredDateTo = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new PurchaseOrderQueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy,
            IsDescending = isDescending,
            Status = status,
            DateFrom = dateFrom,
            DateTo = dateTo,
            RequiredDateFrom = requiredDateFrom,
            RequiredDateTo = requiredDateTo
        };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<PurchaseOrderDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<List<PurchaseOrderDto>>>> GetAllList()
    {
        var result = await _service.GetAllListAsync();
        return Ok(ApiResponse<List<PurchaseOrderDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<PurchaseOrderDto>.Ok(result, "查询成功"));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Policies.MaterialEdit)]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> Create([FromBody] CreatePurchaseOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PurchaseOrderDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<PurchaseOrderDto>.Ok(result, "创建成功"));
    }

    [HttpPost("batch")]
    [Authorize(Roles = Roles.Policies.MaterialEdit)]
    public async Task<ActionResult<ApiResponse<List<PurchaseOrderDto>>>> CreateBatch([FromBody] List<CreatePurchaseOrderRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<PurchaseOrderDto>>.Fail("请求参数无效"));
        if (requests == null || requests.Count == 0)
            return BadRequest(ApiResponse<List<PurchaseOrderDto>>.Fail("请求列表不能为空"));
        var result = await _service.CreateBatchAsync(requests);
        return Ok(ApiResponse<List<PurchaseOrderDto>>.Ok(result, "批量创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.MaterialEdit)]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> Update(int id, [FromBody] UpdatePurchaseOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PurchaseOrderDto>.Fail("请求参数无效"));
        var isAdmin = User.IsInRole(Roles.Admin);
        var result = await _service.UpdateAsync(id, request, isAdmin);
        return Ok(ApiResponse<PurchaseOrderDto>.Ok(result, "更新成功"));
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
        var isAdmin = User.IsInRole(Roles.Admin);
        await _service.DeleteAsync(id, isAdmin);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result, "查询成功"));
    }

    [HttpGet("procurement-status")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<List<ProcurementStatusDto>>>> GetProcurementStatus()
    {
        var result = await _service.GetProcurementStatusAsync();
        return Ok(ApiResponse<List<ProcurementStatusDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("mismatched-orders")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<List<OrderMismatchInfo>>>> GetMismatchedOrders()
    {
        var result = await _service.GetMismatchedPurchaseOrdersAsync();
        return Ok(ApiResponse<List<OrderMismatchInfo>>.Ok(result, "查询成功"));
    }

    // ========== 采购首页汇总（荒管/成品，isFinished=true 成品） ==========

    [HttpGet("summary/pending")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<List<PurchasePendingDto>>>> GetPurchasePending([FromQuery] bool isFinished = false)
    {
        var result = await _service.GetPurchasePendingAsync(isFinished);
        return Ok(ApiResponse<List<PurchasePendingDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("summary/in-progress")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<PurchaseInProgressResultDto>>> GetPurchaseInProgress([FromQuery] bool isFinished = false)
    {
        var result = await _service.GetPurchaseInProgressAsync(isFinished);
        return Ok(ApiResponse<PurchaseInProgressResultDto>.Ok(result, "查询成功"));
    }

    [HttpGet("summary/monthly")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<ActionResult<ApiResponse<PurchaseMonthlyResultDto>>> GetPurchaseMonthly([FromQuery] bool isFinished = false)
    {
        var result = await _service.GetPurchaseMonthlyAsync(isFinished);
        return Ok(ApiResponse<PurchaseMonthlyResultDto>.Ok(result, "查询成功"));
    }

    // ========== 打印 ==========

    [HttpPost("print-batch-file")]
    [Authorize(Roles = Roles.Policies.MaterialView)]
    public async Task<IActionResult> PrintOrderBatchFile([FromBody] OrderPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintOrderBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", $"采购单批量.pdf");
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
