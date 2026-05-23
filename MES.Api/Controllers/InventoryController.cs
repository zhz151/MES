using System.Text.Json;
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
        [FromQuery] InventoryQueryParams query,
        [FromQuery] string? filters = null)
    {
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<InventoryBatchDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 全量查询库存批次（无分页，供前端 Items 模式使用）
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<InventoryBatchDto>>>> GetAll(
        [FromQuery] InventoryQueryParams query,
        [FromQuery] string? filters = null)
    {
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetAllListAsync(query);
        return Ok(ApiResponse<List<InventoryBatchDto>>.Ok(result, "查询成功"));
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
        [FromQuery] OutboundQueryParams query,
        [FromQuery] string? filters = null)
    {
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
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

        var result = await _service.UpdateInventoryBatchAsync(id, request);
        return Ok(ApiResponse<InventoryBatchDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 物理删除入库批次（仅管理员/主任）
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<object>>> HardDeleteInventoryBatch(int id)
    {
        await _service.HardDeleteInventoryBatchAsync(id);
        return Ok(ApiResponse<object>.Ok(null!, "删除成功"));
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

        var result = await _service.UpdateOutboundRecordAsync(id, request);
        return Ok(ApiResponse<OutboundRecordDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 物理删除出库记录（仅管理员/主任）
    /// </summary>
    [HttpDelete("outbound-records/{id:long}")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<object>>> HardDeleteOutboundRecord(long id)
    {
        await _service.HardDeleteOutboundRecordAsync(id);
        return Ok(ApiResponse<object>.Ok(null!, "删除成功"));
    }

    /// <summary>
    /// 验证来源单号
    /// </summary>
    [HttpPost("validate-source-order")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SourceOrderValidationResult>>> ValidateSourceOrder(
        [FromBody] SourceOrderValidationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SourceOrderValidationResult>.Fail("请求参数无效"));

        var result = await _service.ValidateSourceOrderAsync(request.SourceOrderNo, request.InboundSource, request.SourceOrderSequence);
        return Ok(ApiResponse<SourceOrderValidationResult>.Ok(result, "验证完成"));
    }

    /// <summary>
    /// 验证仓库内入库数据中的工单号是否在工单管理上下文中存在
    /// </summary>
    [HttpGet("validate-workorder-nos/{warehouseId}")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<string>>>> ValidateWorkOrderNos(int warehouseId)
    {
        var result = await _service.ValidateWarehouseWorkOrderNosAsync(warehouseId);
        return Ok(ApiResponse<List<string>>.Ok(result, "验证完成"));
    }

    // ========== 打印 ==========

    /// <summary>
    /// 打印全部库存/入库记录
    /// </summary>
    [HttpPost("print-inventory-all")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintInventoryAll([FromBody] InventoryPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintInventoryAllAsync(request);
        return Ok(ApiResponse<string>.Ok(Convert.ToBase64String(pdfBytes), "打印成功"));
    }

    /// <summary>
    /// 打印选中库存/入库记录
    /// </summary>
    [HttpPost("print-inventory-selected")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintInventorySelected([FromBody] InventoryPrintSelectedRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintInventorySelectedAsync(request);
        return Ok(ApiResponse<string>.Ok(Convert.ToBase64String(pdfBytes), "打印成功"));
    }

    /// <summary>
    /// 打印全部出库记录
    /// </summary>
    [HttpPost("print-outbound-all")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintOutboundAll([FromBody] OutboundPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintOutboundAllAsync(request);
        return Ok(ApiResponse<string>.Ok(Convert.ToBase64String(pdfBytes), "打印成功"));
    }

    /// <summary>
    /// 打印选中出库记录
    /// </summary>
    [HttpPost("print-outbound-selected")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintOutboundSelected([FromBody] OutboundPrintSelectedRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintOutboundSelectedAsync(request);
        return Ok(ApiResponse<string>.Ok(Convert.ToBase64String(pdfBytes), "打印成功"));
    }

    // ========== 筛选上下文 ==========

    /// <summary>
    /// 获取出库记录筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    [HttpGet("outbound-filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetOutboundFilterContexts()
    {
        var result = await _service.GetOutboundFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    /// <summary>
    /// 获取库存批次筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    [HttpGet("inventory-filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetInventoryFilterContexts()
    {
        var result = await _service.GetInventoryFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }
}
