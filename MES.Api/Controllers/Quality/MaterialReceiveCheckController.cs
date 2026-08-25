using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.Interfaces.Quality;

namespace MES.Api.Controllers.Quality;

/// <summary>
/// 检验到料（成检到料）控制器 — 批次完成标志
/// </summary>
[ApiController]
[Route("api/material-receive-check")]
[Authorize]
public class MaterialReceiveCheckController : ControllerBase
{
    private readonly IMaterialReceiveCheckService _service;
    private readonly ILogger<MaterialReceiveCheckController> _logger;

    public MaterialReceiveCheckController(IMaterialReceiveCheckService service, ILogger<MaterialReceiveCheckController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// 获取批次的检验到料记录
    /// </summary>
    [HttpGet("{batchId}")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<MaterialReceiveCheckDto>>> GetMaterialReceiveCheck(int batchId)
    {
        var result = await _service.GetMaterialReceiveCheckAsync(batchId);
        if (result == null)
            return Ok(ApiResponse<MaterialReceiveCheckDto>.Ok(null!, "暂无成检到料记录"));
        return Ok(ApiResponse<MaterialReceiveCheckDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建检验到料（批次完成标志）
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ApiResponse<MaterialReceiveCheckDto>>> CreateMaterialReceiveCheck(
        [FromBody] CreateMaterialReceiveCheckRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<MaterialReceiveCheckDto>.Fail("请求参数无效"));
        var result = await _service.CreateMaterialReceiveCheckAsync(request);
        return Ok(ApiResponse<MaterialReceiveCheckDto>.Ok(result, "成检到料创建成功，批次已完成"));
    }

    /// <summary>
    /// 更新检验到料
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse<MaterialReceiveCheckDto>>> UpdateMaterialReceiveCheck(
        int id, [FromBody] UpdateMaterialReceiveCheckRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<MaterialReceiveCheckDto>.Fail("请求参数无效"));
        var result = await _service.UpdateMaterialReceiveCheckAsync(id, request);
        return Ok(ApiResponse<MaterialReceiveCheckDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除检验到料
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.QualityDelete)]
    public async Task<ActionResult<ApiResponse>> DeleteMaterialReceiveCheck(int id)
    {
        await _service.DeleteMaterialReceiveCheckAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    /// <summary>
    /// 跨批次查询所有检验到料记录（分页）
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<PagedResult<MaterialReceiveCheckDto>>>> GetAllMaterialReceiveChecks(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] DateTime? receiveDateFrom = null,
        [FromQuery] DateTime? receiveDateTo = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword, SortBy = sortBy ?? "createdtime", IsDescending = isDescending, ReceiveDateFrom = receiveDateFrom, ReceiveDateTo = receiveDateTo };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }
        var result = await _service.GetAllMaterialReceiveChecksAsync(query);
        return Ok(ApiResponse<PagedResult<MaterialReceiveCheckDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取所有检验到料记录列表（不含分页）
    /// </summary>
    [HttpGet("all-list")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ApiResponse<List<MaterialReceiveCheckDto>>> GetAllMaterialReceiveCheckList()
    {
        var result = await _service.GetAllMaterialReceiveCheckListAsync();
        return ApiResponse<List<MaterialReceiveCheckDto>>.Ok(result);
    }

    /// <summary>
    /// 实时健康汇总（按当前筛选条件统计异常记录数）
    /// </summary>
    [HttpGet("health-summary")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<MaterialCheckHealthSummaryDto>>> GetMaterialCheckHealthSummary(
        [FromQuery] string? keyword = null,
        [FromQuery] DateTime? receiveDateFrom = null,
        [FromQuery] DateTime? receiveDateTo = null,
        [FromQuery] string? filters = null)
    {
        var query = new QueryParams { PageIndex = 1, PageSize = 1, Keyword = keyword, ReceiveDateFrom = receiveDateFrom, ReceiveDateTo = receiveDateTo };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }
        var result = await _service.GetMaterialCheckHealthSummaryAsync(query);
        return Ok(ApiResponse<MaterialCheckHealthSummaryDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取待检验到料批次（成品检验阶段且未创建检验到料记录）
    /// </summary>
    [HttpGet("pending")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<List<PendingMaterialCheckDto>>>> GetPendingMaterialChecks()
    {
        var result = await _service.GetPendingMaterialChecksAsync();
        return Ok(ApiResponse<List<PendingMaterialCheckDto>>.Ok(result));
    }

    /// <summary>
    /// 批量创建检验到料
    /// </summary>
    [HttpPost("batch")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse<List<MaterialReceiveCheckDto>>>> BatchCreateMaterialReceiveChecks(
        [FromBody] List<CreateMaterialReceiveCheckRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<MaterialReceiveCheckDto>>.Fail("请求参数无效"));
        if (requests.Count == 0)
            return BadRequest(ApiResponse<List<MaterialReceiveCheckDto>>.Fail("请求列表不能为空"));
        var result = await _service.BatchCreateMaterialReceiveChecksAsync(requests);
        return Ok(ApiResponse<List<MaterialReceiveCheckDto>>.Ok(result, $"批量成检到料创建成功，共{result.Count}条"));
    }

    /// <summary>
    /// 获取检验到料筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    // ========== 打印 ==========

    /// <summary>
    /// 批量打印检验到料
    /// </summary>
    [HttpPost("print-batch")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<string>>> PrintMaterialCheckBatch([FromBody] MaterialCheckPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintMaterialCheckBatchAsync(request.Ids, request.Columns);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    /// <summary>
    /// 按筛选条件打印全部检验到料
    /// </summary>
    [HttpPost("print-all")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<string>>> PrintMaterialCheckAll([FromBody] MaterialCheckPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintMaterialCheckAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.Columns, request.ReceiveDateFrom, request.ReceiveDateTo);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    /// <summary>
    /// 批量打印检验到料（直接返回 PDF 文件）
    /// </summary>
    [HttpPost("print-batch-file")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<IActionResult> PrintMaterialCheckBatchFile([FromBody] MaterialCheckPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintMaterialCheckBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "检验到料打印.pdf");
    }

    /// <summary>
    /// 按筛选条件打印全部检验到料（直接返回 PDF 文件）
    /// </summary>
    [HttpPost("print-all-file")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<IActionResult> PrintMaterialCheckAllFile([FromBody] MaterialCheckPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var pdfBytes = await _service.PrintMaterialCheckAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.Columns, request.ReceiveDateFrom, request.ReceiveDateTo, request.Filters);
        return File(pdfBytes, "application/pdf", "检验到料列表.pdf");
    }
}
