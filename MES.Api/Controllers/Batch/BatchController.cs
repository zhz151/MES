using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Core.Exceptions;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Batch;

[ApiController]
[Route("api/batch")]
[Authorize]
public class BatchController : ControllerBase
{
    private readonly IBatchService _service;
    private readonly IProductionRecordService _productionRecordService;
    private readonly ILogger<BatchController> _logger;

    public BatchController(IBatchService service, IProductionRecordService productionRecordService, ILogger<BatchController> logger)
    {
        _service = service;
        _productionRecordService = productionRecordService;
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
        [FromQuery] string? validInputQuestion = null,
        [FromQuery] DateTime? startDateFrom = null,
        [FromQuery] DateTime? startDateTo = null,
        [FromQuery] string? filters = null)
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
            ValidInputQuestion = validInputQuestion,
            StartDateFrom = startDateFrom,
            StartDateTo = startDateTo
        };
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
        return Ok(ApiResponse<PagedResult<ProductionBatchListDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("all-list")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<ProductionBatchListDto>>>> GetAllList()
    {
        var result = await _service.GetAllBatchListAsync();
        return Ok(ApiResponse<List<ProductionBatchListDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProductionBatchDetailDto>>> GetById(int id)
    {
        try
        {
            var result = await _service.GetByIdAsync(id);
            return Ok(ApiResponse<ProductionBatchDetailDto>.Ok(result, "查询成功"));
        }
        catch (BusinessException ex)
        {
            return NotFound(ApiResponse<ProductionBatchDetailDto>.Fail(ex.Message));
        }
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

    // ========== 批次跟踪可视化 ==========

    [HttpGet("{id}/tracking")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<BatchTrackingVisualDto>>> GetTrackingVisual(int id)
    {
        try
        {
            var result = await _productionRecordService.GetTrackingVisualAsync(id);
            return Ok(ApiResponse<BatchTrackingVisualDto>.Ok(result, "查询成功"));
        }
        catch (BusinessException ex)
        {
            return NotFound(ApiResponse<BatchTrackingVisualDto>.Fail(ex.Message));
        }
    }

    // ========== 相邻批次导航 ==========

    [HttpGet("{id}/adjacent")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<AdjacentBatchDto>>> GetAdjacentBatch(int id)
    {
        var result = await _service.GetAdjacentBatchAsync(id);
        return Ok(ApiResponse<AdjacentBatchDto>.Ok(result, "查询成功"));
    }

    // ========== 按批次号调取工序组（用于前端快速复制） ==========

    [HttpGet("{batchNo}/process-groups")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<CreateProcessGroupRequest>>>> GetProcessGroupsByBatchNo(string batchNo)
    {
        try
        {
            var result = await _service.GetProcessGroupsByBatchNoAsync(batchNo);
            if (result.Count == 0)
                return NotFound(ApiResponse<List<CreateProcessGroupRequest>>.Fail($"批次号 {batchNo} 不存在或没有工序组"));
            return Ok(ApiResponse<List<CreateProcessGroupRequest>>.Ok(result, "查询成功"));
        }
        catch (BusinessException ex)
        {
            return NotFound(ApiResponse<List<CreateProcessGroupRequest>>.Fail(ex.Message));
        }
    }

    // ========== 批次操作日志 ==========

    [HttpGet("{id}/operation-logs")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<BatchOperationLogDto>>>> GetOperationLogs(int id)
    {
        var result = await _service.GetOperationLogsAsync(id);
        return Ok(ApiResponse<List<BatchOperationLogDto>>.Ok(result, "查询成功"));
    }

    // ========== 筛选上下文 ==========

    [HttpGet("filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result, "查询成功"));
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
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintBatchAllAsync(request);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("print-selected")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintBatchSelected([FromBody] BatchPrintSelectedRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintBatchSelectedAsync(request.Ids, request.Columns);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("print-process-card")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintProcessCard([FromBody] ProcessCardPrintRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintProcessCardAsync(request);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("print-batch-file")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<IActionResult> PrintBatchFile([FromBody] PrintBatchFileRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintBatchAsync(request.Id);
        return File(pdfBytes, "application/pdf", $"生产批次_{request.Id}.pdf");
    }

    [HttpPost("print-all-file")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<IActionResult> PrintBatchAllFile([FromBody] BatchPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintBatchAllAsync(request);
        return File(pdfBytes, "application/pdf", "生产批次列表.pdf");
    }

    [HttpPost("print-selected-file")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<IActionResult> PrintBatchSelectedFile([FromBody] BatchPrintSelectedRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintBatchSelectedAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "生产批次列表.pdf");
    }

    [HttpPost("print-process-card-file")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<IActionResult> PrintProcessCardFile([FromBody] ProcessCardPrintRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintProcessCardAsync(request);
        return File(pdfBytes, "application/pdf", "工艺流转卡.pdf");
    }

}
