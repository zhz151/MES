using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

[ApiController]
[Route("api/purchase-order")]
[Authorize]
public class PurchaseOrderController : ControllerBase
{
    private readonly IPurchaseOrderService _service;
    private readonly ILogger<PurchaseOrderController> _logger;

    public PurchaseOrderController(IPurchaseOrderService service, ILogger<PurchaseOrderController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
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
        [FromQuery] DateTime? requiredDateTo = null)
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
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<PurchaseOrderDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<PurchaseOrderDto>.Ok(result, "查询成功"));
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> Create([FromBody] CreatePurchaseOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PurchaseOrderDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<PurchaseOrderDto>.Ok(result, "创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> Update(int id, [FromBody] UpdatePurchaseOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PurchaseOrderDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<PurchaseOrderDto>.Ok(result, "更新成功"));
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
}
