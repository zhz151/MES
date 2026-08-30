using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Batch;
using MES.Core.Interfaces.Batch;

namespace MES.Api.Controllers.Batch;

/// <summary>
/// 生产记录控制器（内部生产记录/工段委外/委外回收）
/// </summary>
[ApiController]
[Route("api/production-record")]
[Authorize]
public class ProductionRecordController : ControllerBase
{
    private readonly IProductionRecordService _service;

    public ProductionRecordController(IProductionRecordService service)
    {
        _service = service;
    }

    // ========== 内部生产记录 ==========

    /// <summary>
    /// 获取批次的生产记录列表（分页）
    /// </summary>
    [HttpGet("{batchId}/records")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductionRecordDto>>>> GetProductionRecords(
        int batchId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword, SortBy = sortBy ?? "createdtime", IsDescending = isDescending };
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetProductionRecordsAsync(batchId, query);
        return Ok(ApiResponse<PagedResult<ProductionRecordDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建内部生产记录
    /// </summary>
    [HttpPost("record")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<ProductionRecordDto>>> CreateProductionRecord(
        [FromBody] CreateProductionRecordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<ProductionRecordDto>.Fail("请求参数无效"));
        var result = await _service.CreateProductionRecordAsync(request);
        return Ok(ApiResponse<ProductionRecordDto>.Ok(result, "创建成功"));
    }

    /// <summary>
    /// 批量创建内部生产记录
    /// </summary>
    [HttpPost("records/batch")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<ProductionRecordDto>>>> BatchCreateProductionRecords(
        [FromBody] List<CreateProductionRecordRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<ProductionRecordDto>>.Fail("请求参数无效"));
        if (requests.Count == 0)
            return BadRequest(ApiResponse<List<ProductionRecordDto>>.Fail("请求列表不能为空"));
        var result = await _service.BatchCreateProductionRecordsAsync(requests);
        return Ok(ApiResponse<List<ProductionRecordDto>>.Ok(result, "批量创建成功"));
    }

    /// <summary>
    /// 删除内部生产记录
    /// </summary>
    [HttpDelete("record/{id}")]
    [Authorize(Roles = Roles.Policies.BatchDelete)]
    public async Task<ActionResult<ApiResponse>> DeleteProductionRecord(int id)
    {
        await _service.DeleteProductionRecordAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    /// <summary>
    /// 更新内部生产记录
    /// </summary>
    [HttpPut("record/{id}")]
    [Authorize(Roles = Roles.Policies.BatchEdit)]
    public async Task<ActionResult<ApiResponse<ProductionRecordDto>>> UpdateProductionRecord(
        int id, [FromBody] UpdateProductionRecordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<ProductionRecordDto>.Fail("请求参数无效"));
        var result = await _service.UpdateProductionRecordAsync(id, request);
        return Ok(ApiResponse<ProductionRecordDto>.Ok(result, "更新成功"));
    }

    // ========== 工段委外 ==========

    // ========== 跨批次查询（用于独立页面） ==========

    /// <summary>
    /// 跨批次查询所有内部生产记录（分页）
    /// </summary>
    [HttpGet("all/records")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductionRecordDto>>>> GetAllProductionRecords(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] DateTime? execDateFrom = null,
        [FromQuery] DateTime? execDateTo = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword, SortBy = sortBy ?? "createdtime", IsDescending = isDescending, ExecDateFrom = execDateFrom, ExecDateTo = execDateTo };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }
        var result = await _service.GetAllProductionRecordsAsync(query);
        return Ok(ApiResponse<PagedResult<ProductionRecordDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取生产记录筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    [HttpGet("all/filter-contexts")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    /// <summary>
    /// 批量打印生产记录（直接返回 PDF 文件）
    /// </summary>
    [HttpPost("print-batch-file")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<IActionResult> PrintProductionRecordBatchFile([FromBody] ProductionRecordPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintProductionRecordBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "生产记录打印.pdf");
    }

}
