using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.Interfaces.Quality;

namespace MES.Api.Controllers.Quality;

/// <summary>
/// 化学分析控制器
/// </summary>
[ApiController]
[Route("api/chemical-analysis")]
[Authorize]
public class ChemicalAnalysisController : ControllerBase
{
    private readonly IChemicalAnalysisService _service;

    public ChemicalAnalysisController(IChemicalAnalysisService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取化学分析详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<ChemicalAnalysisDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<ChemicalAnalysisDto>.Fail("记录不存在"));
        return Ok(ApiResponse<ChemicalAnalysisDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 分页查询化学分析记录
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ChemicalAnalysisDto>>>> GetAll(
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
            SortBy = sortBy ?? "analysisdate",
            IsDescending = isDescending,
            InspectionDateFrom = inspectionDateFrom,
            InspectionDateTo = inspectionDateTo
        };
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetAllAsync(query);
        return Ok(ApiResponse<PagedResult<ChemicalAnalysisDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 更新化学分析记录
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse<ChemicalAnalysisDto>>> Update(
        int id, [FromBody] UpdateChemicalAnalysisRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<ChemicalAnalysisDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<ChemicalAnalysisDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除化学分析记录
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.QualityDelete)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    /// <summary>
    /// 批量创建化学分析记录
    /// </summary>
    [HttpPost("batch")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse<List<ChemicalAnalysisDto>>>> BatchCreate(
        [FromBody] List<CreateChemicalAnalysisRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<ChemicalAnalysisDto>>.Fail("请求参数无效"));
        if (requests == null || requests.Count == 0)
            return BadRequest(ApiResponse<List<ChemicalAnalysisDto>>.Fail("请求数据不能为空"));
        var result = await _service.BatchCreateAsync(requests);
        return Ok(ApiResponse<List<ChemicalAnalysisDto>>.Ok(result, "批量创建成功"));
    }

    /// <summary>
    /// 获取筛选上下文（各列的 DISTINCT 值）
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result, "查询成功"));
    }

    /// <summary>批量打印选中记录（PDF 文件）</summary>
    [HttpPost("print-batch-file")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<IActionResult> PrintBatchFile([FromBody] ChemicalAnalysisPrintBatchRequest request)
    {
        if (request.Ids.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("请至少选择一条记录"));
        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "化学检验-选中.pdf");
    }

    /// <summary>按搜索条件打印全部记录（PDF 文件）</summary>
    [HttpPost("print-all-file")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<IActionResult> PrintAllFile([FromBody] ChemicalAnalysisPrintAllRequest request)
    {
        var pdfBytes = await _service.PrintAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.Columns, request.InspectionDateFrom, request.InspectionDateTo);
        return File(pdfBytes, "application/pdf", "化学检验-全部.pdf");
    }
}
