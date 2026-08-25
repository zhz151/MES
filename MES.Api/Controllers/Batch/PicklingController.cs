using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Batch;
using MES.Core.Interfaces.Batch;

namespace MES.Api.Controllers.Batch;

/// <summary>
/// 去油/酸洗控制器（入缸记录 + 完工记录）
/// </summary>
[ApiController]
[Route("api/pickling")]
[Authorize]
public class PicklingController : ControllerBase
{
    private readonly IPicklingService _service;
    private readonly ILogger<PicklingController> _logger;

    public PicklingController(IPicklingService service, ILogger<PicklingController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // ========== 入缸记录 ==========

    /// <summary>
    /// 跨批次分页查询入缸记录
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<ActionResult<ApiResponse<PagedResult<PicklingInRecordDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] DateTime? inDateFrom = null,
        [FromQuery] DateTime? inDateTo = null,
        [FromQuery] DateTime? completeDateFrom = null,
        [FromQuery] DateTime? completeDateTo = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = sortBy ?? "createdtime",
            IsDescending = isDescending,
            InDateFrom = inDateFrom,
            InDateTo = inDateTo,
            CompleteDateFrom = completeDateFrom,
            CompleteDateTo = completeDateTo
        };
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<PicklingInRecordDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建入缸记录
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PicklingInRecordDto>>> Create([FromBody] CreatePicklingInRecordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PicklingInRecordDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<PicklingInRecordDto>.Ok(result, "创建成功"));
    }

    /// <summary>
    /// 批量创建入缸记录
    /// </summary>
    [HttpPost("batch")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<PicklingInRecordDto>>>> BatchCreate(
        [FromBody] List<CreatePicklingInRecordRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<PicklingInRecordDto>>.Fail("请求参数无效"));
        if (requests.Count == 0)
            return BadRequest(ApiResponse<List<PicklingInRecordDto>>.Fail("请求列表不能为空"));
        var result = await _service.BatchCreateAsync(requests);
        return Ok(ApiResponse<List<PicklingInRecordDto>>.Ok(result, "批量创建成功"));
    }

    /// <summary>
    /// 更新入缸记录（内联编辑）
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.BatchEdit)]
    public async Task<ActionResult<ApiResponse<PicklingInRecordDto>>> Update(int id, [FromBody] UpdatePicklingInRecordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PicklingInRecordDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<PicklingInRecordDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除入缸记录
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.BatchDelete)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    // ========== 按批次查询 ==========

    /// <summary>
    /// 按批次号查询入缸记录（用于出缸扫码时选择关联的入缸记录）
    /// </summary>
    [HttpGet("by-batch/{batchNo}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<PicklingInRecordDto>>>> GetByBatch(string batchNo)
    {
        var result = await _service.GetByBatchAsync(batchNo);
        return Ok(ApiResponse<List<PicklingInRecordDto>>.Ok(result, "查询成功"));
    }

    // ========== 完工记录 ==========

    /// <summary>
    /// 获取指定入缸的完工记录
    /// </summary>
    [HttpGet("{picklingInRecordId}/out-record")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<ActionResult<ApiResponse<PicklingOutRecordDto?>>> GetOutRecordByInId(int picklingInRecordId)
    {
        var result = await _service.GetOutRecordByInIdAsync(picklingInRecordId);
        return Ok(ApiResponse<PicklingOutRecordDto?>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 跨批次分页查询完工记录
    /// </summary>
    [HttpGet("out-records/list")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<ActionResult<ApiResponse<PagedResult<PicklingOutRecordDto>>>> GetOutRecordsPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] DateTime? completeDateFrom = null,
        [FromQuery] DateTime? completeDateTo = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = sortBy ?? "completedate",
            IsDescending = isDescending,
            CompleteDateFrom = completeDateFrom,
            CompleteDateTo = completeDateTo
        };
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetOutRecordsPagedAsync(query);
        return Ok(ApiResponse<PagedResult<PicklingOutRecordDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建完工记录（自动更新入缸状态为 Completed）
    /// </summary>
    [HttpPost("out-record")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PicklingOutRecordDto>>> CreateOutRecord(
        [FromBody] CreatePicklingOutRecordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PicklingOutRecordDto>.Fail("请求参数无效"));
        var result = await _service.CreateOutRecordAsync(request);
        return Ok(ApiResponse<PicklingOutRecordDto>.Ok(result, "创建成功"));
    }

    /// <summary>
    /// 更新完工记录
    /// </summary>
    [HttpPut("out-record/{id}")]
    [Authorize(Roles = Roles.Policies.BatchEdit)]
    public async Task<ActionResult<ApiResponse<PicklingOutRecordDto>>> UpdateOutRecord(int id, [FromBody] UpdatePicklingOutRecordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PicklingOutRecordDto>.Fail("请求参数无效"));
        var result = await _service.UpdateOutRecordAsync(id, request);
        return Ok(ApiResponse<PicklingOutRecordDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除完工记录
    /// </summary>
    [HttpDelete("out-record/{id}")]
    [Authorize(Roles = Roles.Policies.BatchDelete)]
    public async Task<ActionResult<ApiResponse>> DeleteOutRecord(int id)
    {
        await _service.DeleteOutRecordAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    // ========== 打印 ==========

    /// <summary>
    /// 批量打印入缸记录（选中）
    /// </summary>
    [HttpPost("print-selected")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<ActionResult<ApiResponse<string>>> PrintSelected([FromBody] PicklingInRecordPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    /// <summary>
    /// 按筛选条件打印全部入缸记录
    /// </summary>
    [HttpPost("print-all")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<ActionResult<ApiResponse<string>>> PrintAll([FromBody] PicklingInRecordPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintAllAsync(request.Keyword, request.SortBy, request.IsDescending,
            request.InDateFrom, request.InDateTo,
            request.CompleteDateFrom, request.CompleteDateTo,
            request.Columns);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    /// <summary>
    /// 批量打印入缸记录（选中，直接返回 PDF 文件）
    /// </summary>
    [HttpPost("print-selected-file")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<IActionResult> PrintSelectedFile([FromBody] PicklingInRecordPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "酸洗入缸记录打印.pdf");
    }

    /// <summary>
    /// 按筛选条件打印全部入缸记录（直接返回 PDF 文件）
    /// </summary>
    [HttpPost("print-all-file")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<IActionResult> PrintAllFile([FromBody] PicklingInRecordPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintAllAsync(request.Keyword, request.SortBy, request.IsDescending,
            request.InDateFrom, request.InDateTo,
            request.CompleteDateFrom, request.CompleteDateTo,
            request.Columns);
        return File(pdfBytes, "application/pdf", "酸洗入缸记录列表.pdf");
    }

    // ========== 完工记录打印 ==========

    /// <summary>
    /// 批量打印完工记录（选中，直接返回 PDF 文件）
    /// </summary>
    [HttpPost("out-records/print-selected-file")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<IActionResult> PrintOutSelectedFile([FromBody] PicklingOutRecordPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintOutBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "酸洗完工记录打印.pdf");
    }

    /// <summary>
    /// 按筛选条件打印全部完工记录（直接返回 PDF 文件）
    /// </summary>
    [HttpPost("out-records/print-all-file")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<IActionResult> PrintOutAllFile([FromBody] PicklingOutRecordPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintOutAllAsync(request.Keyword, request.SortBy, request.IsDescending,
            request.CompleteDateFrom, request.CompleteDateTo,
            request.Columns);
        return File(pdfBytes, "application/pdf", "酸洗完工记录列表.pdf");
    }

    // ========== 筛选上下文 ==========

    /// <summary>
    /// 获取入缸记录筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    /// <summary>
    /// 获取完工记录筛选上下文
    /// </summary>
    [HttpGet("out-records/filter-contexts")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetOutRecordFilterContexts()
    {
        var result = await _service.GetOutRecordFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }
}
