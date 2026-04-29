using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _service;

    public InventoryController(IInventoryService service)
    {
        _service = service;
    }

    /// <summary>
    /// 分页查询库存列表
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<InventoryBatchDto>>>> GetPaged(
        [FromQuery] InventoryQueryParams query)
    {
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<InventoryBatchDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取批次详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<InventoryBatchDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<InventoryBatchDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 批量入库
    /// </summary>
    [HttpPost("batch-inbound")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<BatchInboundResult>>> BatchInbound([FromBody] BatchInboundRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<BatchInboundResult>.Fail("请求参数无效"));

        var result = await _service.BatchInboundAsync(request);
        return Ok(ApiResponse<BatchInboundResult>.Ok(result, $"批量入库成功，共{result.SuccessCount}条"));
    }

    /// <summary>
    /// 入库
    /// </summary>
    [HttpPost("inbound")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<InventoryBatchDto>>> Inbound([FromBody] CreateInboundRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<InventoryBatchDto>.Fail("请求参数无效"));

        var result = await _service.InboundAsync(request);
        return Ok(ApiResponse<InventoryBatchDto>.Ok(result, "入库成功"));
    }

    /// <summary>
    /// 出库
    /// </summary>
    [HttpPost("outbound")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<OutboundRecordDto>>> Outbound([FromBody] CreateOutboundRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<OutboundRecordDto>.Fail("请求参数无效"));

        var result = await _service.OutboundAsync(request);
        return Ok(ApiResponse<OutboundRecordDto>.Ok(result, "出库成功"));
    }

    /// <summary>
    /// 批量出库
    /// </summary>
    [HttpPost("batch-outbound")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<BatchOutboundResult>>> BatchOutbound([FromBody] BatchOutboundRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<BatchOutboundResult>.Fail("请求参数无效"));

        var result = await _service.BatchOutboundAsync(request);
        return Ok(ApiResponse<BatchOutboundResult>.Ok(result, $"批量出库成功，共{result.SuccessCount}条"));
    }

    /// <summary>
    /// 查询出库记录
    /// </summary>
    [HttpGet("outbound-records")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<OutboundRecordDto>>>> GetOutboundRecords(
        [FromQuery] OutboundQueryParams query)
    {
        var result = await _service.GetOutboundRecordsAsync(query);
        return Ok(ApiResponse<PagedResult<OutboundRecordDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 更新入库批次
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<InventoryBatchDto>>> UpdateInventoryBatch(int id, [FromBody] UpdateInventoryBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<InventoryBatchDto>.Fail("请求参数无效"));

        try
        {
            var result = await _service.UpdateInventoryBatchAsync(id, request);
            return Ok(ApiResponse<InventoryBatchDto>.Ok(result, "更新成功"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<InventoryBatchDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 物理删除入库批次（仅管理员/主任）
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<object>>> HardDeleteInventoryBatch(int id)
    {
        try
        {
            await _service.HardDeleteInventoryBatchAsync(id);
            return Ok(ApiResponse<object>.Ok(null!, "删除成功"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 更新出库记录
    /// </summary>
    [HttpPut("outbound-records/{id:long}")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<OutboundRecordDto>>> UpdateOutboundRecord(long id, [FromBody] UpdateOutboundRecordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<OutboundRecordDto>.Fail("请求参数无效"));

        try
        {
            var result = await _service.UpdateOutboundRecordAsync(id, request);
            return Ok(ApiResponse<OutboundRecordDto>.Ok(result, "更新成功"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<OutboundRecordDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 物理删除出库记录（仅管理员/主任）
    /// </summary>
    [HttpDelete("outbound-records/{id:long}")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<object>>> HardDeleteOutboundRecord(long id)
    {
        try
        {
            await _service.HardDeleteOutboundRecordAsync(id);
            return Ok(ApiResponse<object>.Ok(null!, "删除成功"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
