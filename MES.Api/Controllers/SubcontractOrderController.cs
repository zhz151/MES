using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

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
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<SubcontractOrderDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? status = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new SubcontractQueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy,
            IsDescending = isDescending,
            Status = status
        };
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<SubcontractOrderDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SubcontractOrderDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<SubcontractOrderDto>.Ok(result, "查询成功"));
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SubcontractOrderDto>>> Create([FromBody] CreateSubcontractOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SubcontractOrderDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<SubcontractOrderDto>.Ok(result, "创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SubcontractOrderDto>>> Update(int id, [FromBody] UpdateSubcontractOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SubcontractOrderDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<SubcontractOrderDto>.Ok(result, "更新成功"));
    }

    [HttpPost("sync-all")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> SyncAll()
    {
        await _service.SyncAllAsync();
        return Ok(ApiResponse.Ok("同步完成"));
    }

    [HttpPost("{id}/sync")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> SyncSingle(int id)
    {
        await _service.SyncSingleAsync(id);
        return Ok(ApiResponse.Ok("同步完成"));
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = $"{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        await _service.UpdateStatusAsync(id, request);
        return Ok(ApiResponse.Ok("状态更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpGet("procurement-status")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<ProcurementStatusDto>>>> GetProcurementStatus()
    {
        var result = await _service.GetProcurementStatusAsync();
        return Ok(ApiResponse<List<ProcurementStatusDto>>.Ok(result, "查询成功"));
    }

    // ========== 打印 ==========

    [HttpGet("{id}/print")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintOrder(int id)
    {
        var pdfBytes = await _service.PrintOrderAsync(id);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("print-batch")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintOrderBatch([FromBody] OrderPrintBatchRequest request)
    {
        var pdfBytes = await _service.PrintOrderBatchAsync(request.Ids);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("print-all")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintOrderAll([FromBody] OrderPrintAllRequest request)
    {
        var pdfBytes = await _service.PrintOrderAllAsync(request.Keyword, request.SortBy, request.IsDescending);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpGet("plan-detail")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PlanDetailDto>>> GetPlanDetail(
        [FromQuery] string workOrderNo, [FromQuery] string materialCategory)
    {
        var result = await _service.GetPlanDetailAsync(workOrderNo, materialCategory);
        if (result == null)
            return Ok(ApiResponse<PlanDetailDto>.Fail("未找到对应的用料计划"));
        return Ok(ApiResponse<PlanDetailDto>.Ok(result, "查询成功"));
    }
}
