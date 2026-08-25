using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.StandardRegister;
using MES.Core.Interfaces.StandardRegister;

namespace MES.Api.Controllers.StandardRegister;

[ApiController]
[Route("api/grade-physical-property")]
[Authorize]
public class GradePhysicalPropertyController : ControllerBase
{
    private readonly IGradePhysicalPropertyService _service;

    public GradePhysicalPropertyController(IGradePhysicalPropertyService service) => _service = service;

    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.StandardView)]
    public async Task<ActionResult<ApiResponse<PagedResult<GradePhysicalPropertyDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null, [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true, [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword, SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy, IsDescending = isDescending };
        if (!string.IsNullOrEmpty(filters))
        {
            try { var f = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); if (f?.Count > 0) query.Filters = f; } catch { }
        }
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<GradePhysicalPropertyDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.StandardView)]
    public async Task<ActionResult<ApiResponse<GradePhysicalPropertyDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<GradePhysicalPropertyDto>.Ok(result, "查询成功"));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Policies.StandardEdit)]
    public async Task<ActionResult<ApiResponse<GradePhysicalPropertyDto>>> Create([FromBody] CreateGradePhysicalPropertyRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<GradePhysicalPropertyDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<GradePhysicalPropertyDto>.Ok(result, "创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.StandardEdit)]
    public async Task<ActionResult<ApiResponse<GradePhysicalPropertyDto>>> Update(int id, [FromBody] UpdateGradePhysicalPropertyRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<GradePhysicalPropertyDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<GradePhysicalPropertyDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.StandardDelete)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse<bool>.Ok(true, "删除成功"));
    }

    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.StandardView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    // ========== 打印 ==========

    /// <summary>批量打印选中记录（PDF 文件）</summary>
    [HttpPost("print-batch-file")]
    [Authorize(Roles = Roles.Policies.StandardView)]
    public async Task<IActionResult> PrintBatchFile([FromBody] GradePhysicalPropertyPrintBatchRequest request)
    {
        if (request.Ids.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("请至少选择一条记录"));
        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "牌号物性-选中.pdf");
    }

    /// <summary>按搜索条件打印全部记录（PDF 文件）</summary>
    [HttpPost("print-all-file")]
    [Authorize(Roles = Roles.Policies.StandardView)]
    public async Task<IActionResult> PrintAllFile([FromBody] GradePhysicalPropertyPrintAllRequest request)
    {
        var pdfBytes = await _service.PrintAllAsync(request.Keyword, request.SortBy, request.IsDescending, request.Columns);
        return File(pdfBytes, "application/pdf", "牌号物性-全部.pdf");
    }
}
