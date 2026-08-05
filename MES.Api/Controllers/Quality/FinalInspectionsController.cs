using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.Interfaces.Quality;

namespace MES.Api.Controllers.Quality;

/// <summary>
/// 成品检验控制器
/// </summary>
[ApiController]
[Route("api/final-inspection")]
[Authorize]
public class FinalInspectionsController : ControllerBase
{
    private readonly IFinalInspectionService _service;
    private readonly ILogger<FinalInspectionsController> _logger;

    public FinalInspectionsController(IFinalInspectionService service, ILogger<FinalInspectionsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// 获取成品检验详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<FinalInspectionDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<FinalInspectionDto>.Fail("记录不存在"));
        return Ok(ApiResponse<FinalInspectionDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 分页查询成品检验记录
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<FinalInspectionDto>>>> GetAll(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = false,
        [FromQuery] DateTime? inspectionDateFrom = null,
        [FromQuery] DateTime? inspectionDateTo = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = sortBy ?? "inspectiondate",
            IsDescending = isDescending,
            InspectionDateFrom = inspectionDateFrom,
            InspectionDateTo = inspectionDateTo
        };
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetAllAsync(query);
        return Ok(ApiResponse<PagedResult<FinalInspectionDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 实时健康汇总（按当前筛选条件统计成检类型与成检到料不符的生产编号）
    /// </summary>
    [HttpGet("health-summary")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<FinalInspectionHealthSummaryDto>>> GetHealthSummary(
        [FromQuery] string? keyword = null,
        [FromQuery] DateTime? inspectionDateFrom = null,
        [FromQuery] DateTime? inspectionDateTo = null,
        [FromQuery] string? filters = null)
    {
        var query = new QueryParams
        {
            Keyword = keyword,
            InspectionDateFrom = inspectionDateFrom,
            InspectionDateTo = inspectionDateTo
        };
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetFinalInspectionHealthSummaryAsync(query);
        return Ok(ApiResponse<FinalInspectionHealthSummaryDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取所有成品检验记录（无分页）
    /// </summary>
    [HttpGet("all-list")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ApiResponse<List<FinalInspectionDto>>> GetAllList()
    {
        var result = await _service.GetAllListAsync();
        return ApiResponse<List<FinalInspectionDto>>.Ok(result);
    }

    /// <summary>
    /// 创建成品检验记录
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<FinalInspectionDto>>> Create(
        [FromBody] CreateFinalInspectionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<FinalInspectionDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<FinalInspectionDto>.Ok(result, "创建成功"));
    }

    /// <summary>
    /// 更新成品检验记录
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<FinalInspectionDto>>> Update(
        int id, [FromBody] UpdateFinalInspectionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<FinalInspectionDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<FinalInspectionDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除成品检验记录
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    /// <summary>
    /// 批量创建成品检验记录
    /// </summary>
    [HttpPost("batch")]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<FinalInspectionDto>>>> BatchCreate(
        [FromBody] List<CreateFinalInspectionRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<FinalInspectionDto>>.Fail("请求参数无效"));
        if (requests == null || requests.Count == 0)
            return BadRequest(ApiResponse<List<FinalInspectionDto>>.Fail("请求数据不能为空"));
        var result = await _service.BatchCreateAsync(requests);
        return Ok(ApiResponse<List<FinalInspectionDto>>.Ok(result, "批量创建成功"));
    }

    /// <summary>
    /// 获取筛选上下文（各列的 DISTINCT 值）
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 根据生产编号调取批次关联信息
    /// </summary>
    [HttpGet("lookup-batch")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<BatchLookupResultDto?>>> LookupBatch(
        [FromQuery] string batchNo)
    {
        if (string.IsNullOrWhiteSpace(batchNo))
            return Ok(ApiResponse<BatchLookupResultDto?>.Ok(null, "查询成功"));
        var result = await _service.LookupBatchAsync(batchNo);
        return Ok(ApiResponse<BatchLookupResultDto?>.Ok(result, "查询成功"));
    }

    /// <summary>批量打印选中记录（PDF 文件）</summary>
    [HttpPost("print-batch-file")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<IActionResult> PrintBatchFile([FromBody] FinalInspectionPrintBatchRequest request)
    {
        if (request.Ids.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("请至少选择一条记录"));
        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "成品检验-选中.pdf");
    }

    /// <summary>按搜索条件打印全部记录（PDF 文件）</summary>
    [HttpPost("print-all-file")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<IActionResult> PrintAllFile([FromBody] FinalInspectionPrintAllRequest request)
    {
        var pdfBytes = await _service.PrintAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.Columns, request.InspectionDateFrom, request.InspectionDateTo, request.Filters);
        return File(pdfBytes, "application/pdf", "成品检验-全部.pdf");
    }
}
