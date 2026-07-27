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
    private readonly ILogger<ProductionRecordController> _logger;

    public ProductionRecordController(IProductionRecordService service, ILogger<ProductionRecordController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // ========== 内部生产记录 ==========

    /// <summary>
    /// 获取批次的生产记录列表（分页）
    /// </summary>
    [HttpGet("{batchId}/records")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
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
    [Authorize(Roles = $"{Roles.Directors.Batch},{Roles.Admin}")]
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
    [Authorize(Roles = $"{Roles.Directors.Batch},{Roles.Admin}")]
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
    [Authorize(Roles = $"{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> DeleteProductionRecord(int id)
    {
        await _service.DeleteProductionRecordAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    /// <summary>
    /// 更新内部生产记录
    /// </summary>
    [HttpPut("record/{id}")]
    [Authorize(Roles = $"{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProductionRecordDto>>> UpdateProductionRecord(
        int id, [FromBody] UpdateProductionRecordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<ProductionRecordDto>.Fail("请求参数无效"));
        var result = await _service.UpdateProductionRecordAsync(id, request);
        return Ok(ApiResponse<ProductionRecordDto>.Ok(result, "更新成功"));
    }

    // ========== 工段委外 ==========

    /// <summary>
    /// 获取批次的工段委外列表（分页）
    /// </summary>
    [HttpGet("{batchId}/outsources")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<SectionOutsourceDto>>>> GetSectionOutsources(
        int batchId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams { PageIndex = pageIndex, PageSize = pageSize };
        var result = await _service.GetSectionOutsourcesAsync(batchId, query);
        return Ok(ApiResponse<PagedResult<SectionOutsourceDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 刷新批次跟踪字段
    /// </summary>
    [HttpPost("{batchId}/refresh-tracking")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> RefreshBatchTracking(int batchId)
    {
        await _service.RefreshBatchTrackingFieldsAsync(batchId);
        return Ok(ApiResponse.Ok("跟踪字段已刷新"));
    }

    /// <summary>
    /// 刷新全部批次跟踪字段（一次查询 + 一次保存）
    /// </summary>
    [HttpPost("refresh-all-tracking")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> RefreshAllBatchTracking()
    {
        var count = await _service.RefreshAllBatchTrackingAsync();
        return Ok(ApiResponse.Ok($"已刷新 {count} 个批次跟踪字段"));
    }

    /// <summary>
    /// 删除生产记录中所有"去油"和"酸洗"的旧数据（已被 PicklingInRecord 替代）
    /// </summary>
    [HttpDelete("cleanup-degrease-pickle")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> CleanupDegreasePickleRecords()
    {
        var count = await _service.CleanupDegreasePickleRecordsAsync();
        return Ok(ApiResponse.Ok($"已删除 {count} 条去油/酸洗生产记录"));
    }

    // ========== 跨批次查询（用于独立页面） ==========

    /// <summary>
    /// 跨批次查询所有内部生产记录（分页）
    /// </summary>
    [HttpGet("all/records")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
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
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    /// <summary>
    /// 获取所有内部生产记录列表（不含分页，用于 ProductionRecords 页面）
    /// </summary>
    [HttpGet("all-list")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ApiResponse<List<ProductionRecordDto>>> GetAllProductionRecordList()
    {
        var result = await _service.GetAllProductionRecordListAsync();
        return ApiResponse<List<ProductionRecordDto>>.Ok(result);
    }

    /// <summary>
    /// 跨批次查询所有工段委外记录（分页）
    /// </summary>
    [HttpGet("all/outsources")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<SectionOutsourceDto>>>> GetAllSectionOutsources(
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
        var result = await _service.GetAllSectionOutsourcesAsync(query);
        return Ok(ApiResponse<PagedResult<SectionOutsourceDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 跨批次查询所有委外回收记录（分页）
    /// </summary>
    [HttpGet("all/recoveries")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<OutsourceRecoveryDto>>>> GetAllOutsourceRecoveries(
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
        var result = await _service.GetAllOutsourceRecoveriesAsync(query);
        return Ok(ApiResponse<PagedResult<OutsourceRecoveryDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取所有工段委外记录列表（不含分页，用于 SectionOutsources 页面）
    /// </summary>
    [HttpGet("section-outsources/all-list")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ApiResponse<List<SectionOutsourceDto>>> GetAllSectionOutsourceList()
    {
        var result = await _service.GetAllSectionOutsourceListAsync();
        return ApiResponse<List<SectionOutsourceDto>>.Ok(result);
    }

    /// <summary>
    /// 获取所有委外回收记录列表（不含分页，用于 OutsourceRecoveries 页面）
    /// </summary>
    [HttpGet("outsource-recoveries/all-list")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ApiResponse<List<OutsourceRecoveryDto>>> GetAllOutsourceRecoveryList()
    {
        var result = await _service.GetAllOutsourceRecoveryListAsync();
        return ApiResponse<List<OutsourceRecoveryDto>>.Ok(result);
    }

    /// <summary>
    /// 批量打印生产记录
    /// </summary>
    [HttpPost("print-batch")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintProductionRecordBatch([FromBody] ProductionRecordPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintProductionRecordBatchAsync(request.Ids, request.Columns);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    /// <summary>
    /// 按筛选条件打印全部生产记录
    /// </summary>
    [HttpPost("print-all")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintProductionRecordAll([FromBody] ProductionRecordPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintProductionRecordAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.Columns, request.ExecDateFrom, request.ExecDateTo);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    [HttpPost("print-batch-file")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<IActionResult> PrintProductionRecordBatchFile([FromBody] ProductionRecordPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintProductionRecordBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "生产记录打印.pdf");
    }

    [HttpPost("print-all-file")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<IActionResult> PrintProductionRecordAllFile([FromBody] ProductionRecordPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintProductionRecordAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.Columns, request.ExecDateFrom, request.ExecDateTo);
        return File(pdfBytes, "application/pdf", "生产记录列表.pdf");
    }
}
