using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Core.Exceptions;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

[ApiController]
[Route("api/batch")]
[Authorize]
public class BatchController : ControllerBase
{
    private readonly IBatchService _service;
    private readonly ILogger<BatchController> _logger;

    public BatchController(IBatchService service, ILogger<BatchController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // ========== 批次 CRUD ==========

    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductionBatchListDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? workOrderNo = null,
        [FromQuery] string? status = null,
        [FromQuery] string? tagNo = null,
        [FromQuery] string? batchNo = null,
        [FromQuery] DateTime? startDateFrom = null,
        [FromQuery] DateTime? startDateTo = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new BatchQueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy,
            IsDescending = isDescending,
            WorkOrderNo = workOrderNo,
            Status = status,
            TagNo = tagNo,
            BatchNo = batchNo,
            StartDateFrom = startDateFrom,
            StartDateTo = startDateTo
        };
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<ProductionBatchListDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProductionBatchDetailDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<ProductionBatchDetailDto>.Ok(result, "查询成功"));
    }

    [HttpGet("by-batch-no/{batchNo}")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProductionBatchDetailDto>>> GetByBatchNo(string batchNo)
    {
        try
        {
            var result = await _service.GetByBatchNoAsync(batchNo);
            return Ok(ApiResponse<ProductionBatchDetailDto>.Ok(result, "查询成功"));
        }
        catch (BusinessException ex)
        {
            return NotFound(ApiResponse<ProductionBatchDetailDto>.Fail(ex.Message));
        }
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProductionBatchListDto>>> Create([FromBody] CreateProductionBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<ProductionBatchListDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<ProductionBatchListDto>.Ok(result, "创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProductionBatchDetailDto>>> Update(int id, [FromBody] UpdateProductionBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<ProductionBatchDetailDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<ProductionBatchDetailDto>.Ok(result, "更新成功"));
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> UpdateStatus(int id, [FromBody] UpdateBatchStatusRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse.Fail("请求参数无效"));
        await _service.UpdateStatusAsync(id, request);
        return Ok(ApiResponse.Ok("状态更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpPost("{id}/save-all")]
    [Authorize(Roles = $"{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SaveBatchResponse>>> SaveAll(int id, [FromBody] SaveBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SaveBatchResponse>.Fail("请求参数无效"));
        var result = await _service.SaveAllAsync(id, request);
        return Ok(ApiResponse<SaveBatchResponse>.Ok(result, "批量保存成功"));
    }

    // ========== 工序组 ==========

    [HttpGet("{batchId}/records")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<ProcessGroupDto>>>> GetProcessGroups(int batchId)
    {
        var result = await _service.GetProcessGroupsAsync(batchId);
        return Ok(ApiResponse<List<ProcessGroupDto>>.Ok(result, "查询成功"));
    }

    [HttpPost("{batchId}/records")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProcessGroupDto>>> AddProcessGroup(int batchId, [FromBody] CreateProcessGroupRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<ProcessGroupDto>.Fail("请求参数无效"));
        var result = await _service.AddProcessGroupAsync(batchId, request);
        return Ok(ApiResponse<ProcessGroupDto>.Ok(result, "添加工序组成功"));
    }

    [HttpDelete("records/{recordId}")]
    [Authorize(Roles = $"{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> DeleteProcessGroup(int recordId)
    {
        await _service.DeleteProcessGroupAsync(recordId);
        return Ok(ApiResponse.Ok("删除工序组成功"));
    }

    // ========== 查询 ==========

    [HttpGet("available-batches")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<AvailableBatchDto>>>> GetAvailableBatches()
    {
        var result = await _service.GetAvailableBatchesAsync();
        return Ok(ApiResponse<List<AvailableBatchDto>>.Ok(result, "查询成功"));
    }

    // ========== 复制上个工序组 ==========

    [HttpGet("last-process-groups")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<CreateProcessGroupRequest>>>> GetLastBatchProcessGroups()
    {
        var result = await _service.GetLastBatchProcessGroupsAsync();
        return Ok(ApiResponse<List<CreateProcessGroupRequest>>.Ok(result, "查询成功"));
    }

    // ========== 编号生成 ==========

    [HttpGet("next-batch-no")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> GetNextBatchNo()
    {
        var result = await _service.GetNextBatchNoAsync();
        return Ok(ApiResponse<string>.Ok(result, "查询成功"));
    }

    // ========== 工单号验证 ==========

    [HttpGet("verify-workorders")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<BatchWorkOrderMismatchDto>>>> VerifyWorkOrderNos()
    {
        var result = await _service.VerifyWorkOrderNosAsync();
        return Ok(ApiResponse<List<BatchWorkOrderMismatchDto>>.Ok(result, "验证完成"));
    }

    // ========== 按工单号查询 ==========

    [HttpGet("by-work-order/{workOrderNo}")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<ProductionBatchListDto>>>> GetByWorkOrderNo(string workOrderNo)
    {
        var query = new BatchQueryParams { WorkOrderNo = workOrderNo, PageSize = 5000 };
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<List<ProductionBatchListDto>>.Ok(result.Items.ToList(), "查询成功"));
    }

    // ========== 打印 ==========

    [HttpGet("{id}/print")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintBatch(int id)
    {
        var pdfBytes = await _service.PrintBatchAsync(id);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("print-all")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintBatchAll([FromBody] BatchPrintAllRequest request)
    {
        var pdfBytes = await _service.PrintBatchAllAsync(request);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("print-selected")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintBatchSelected([FromBody] int[] ids)
    {
        var pdfBytes = await _service.PrintBatchSelectedAsync(ids);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("print-process-card")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintProcessCard([FromBody] ProcessCardPrintRequest request)
    {
        var pdfBytes = await _service.PrintProcessCardAsync(request);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }
}
