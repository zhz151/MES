using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Quality;

/// <summary>
/// 过程检验控制器
/// </summary>
[ApiController]
[Route("api/process-inspection")]
[Authorize]
public class ProcessInspectionController : ControllerBase
{
    private readonly IProcessInspectionService _service;
    private readonly ILogger<ProcessInspectionController> _logger;

    public ProcessInspectionController(IProcessInspectionService service, ILogger<ProcessInspectionController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// 跨批次查询所有过程检验记录（分页）
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<ProcessInspectionDto>>>> GetAll(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
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
            SortBy = sortBy ?? "createdtime",
            IsDescending = isDescending,
            InspectionDateFrom = inspectionDateFrom,
            InspectionDateTo = inspectionDateTo
        };
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetAllAsync(query);
        return Ok(ApiResponse<PagedResult<ProcessInspectionDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取所有过程检验记录（无分页）
    /// </summary>
    [HttpGet("all-list")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ApiResponse<List<ProcessInspectionDto>>> GetAllList()
    {
        var result = await _service.GetAllListAsync();
        return ApiResponse<List<ProcessInspectionDto>>.Ok(result);
    }

    /// <summary>
    /// 获取筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    /// <summary>
    /// 批量创建过程检验记录
    /// </summary>
    [HttpPost("batch")]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<ProcessInspectionDto>>>> BatchCreate(
        [FromBody] List<CreateProcessInspectionRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<ProcessInspectionDto>>.Fail("请求参数无效"));
        if (requests.Count == 0)
            return BadRequest(ApiResponse<List<ProcessInspectionDto>>.Fail("请求列表不能为空"));
        var result = await _service.BatchCreateAsync(requests);
        return Ok(ApiResponse<List<ProcessInspectionDto>>.Ok(result, "批量创建成功"));
    }

    /// <summary>
    /// 更新过程检验记录
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProcessInspectionDto>>> Update(
        int id, [FromBody] UpdateProcessInspectionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<ProcessInspectionDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<ProcessInspectionDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除过程检验记录
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    /// <summary>批量打印选中记录（PDF 文件）</summary>
    [HttpPost("print-batch-file")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<IActionResult> PrintBatchFile([FromBody] ProcessInspectionPrintBatchRequest request)
    {
        if (request.Ids.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("请至少选择一条记录"));
        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "过程检验-选中.pdf");
    }

    /// <summary>按搜索条件打印全部记录（PDF 文件）</summary>
    [HttpPost("print-all-file")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<IActionResult> PrintAllFile([FromBody] ProcessInspectionPrintAllRequest request)
    {
        var pdfBytes = await _service.PrintAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.Columns);
        return File(pdfBytes, "application/pdf", "过程检验-全部.pdf");
    }
}
