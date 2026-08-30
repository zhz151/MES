using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.Configuration;
using System.Text.Json;

namespace MES.Api.Controllers.Configuration;

[ApiController]
[Route("api/workstation")]
[Authorize]
public class WorkstationController : ControllerBase
{
    private readonly IWorkstationService _workstationService;

    public WorkstationController(IWorkstationService workstationService)
    {
        _workstationService = workstationService;
    }

    /// <summary>
    /// 分页查询
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.ScanView)]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkstationDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        QueryParams query = new() { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword, SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy, IsDescending = isDescending };
        if (!string.IsNullOrEmpty(filters))
            query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var result = await _workstationService.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<WorkstationDto>>.Ok(result));
    }

    /// <summary>
    /// 列头筛选上下文（ExcelFilter 下拉选项）
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.ScanView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _workstationService.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 按工位编码查询（扫码用）
    /// </summary>
    [HttpGet("{code}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<WorkstationDto>>> GetByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(ApiResponse<WorkstationDto>.Fail("工位编码不能为空"));

        var result = await _workstationService.GetByCodeAsync(code);
        if (result == null)
            return NotFound(ApiResponse<WorkstationDto>.Fail("未找到工位"));

        return Ok(ApiResponse<WorkstationDto>.Ok(result));
    }

    /// <summary>
    /// 新增或更新
    /// </summary>
    [HttpPost("save")]
    [Authorize(Roles = Roles.Policies.ScanEdit)]
    public async Task<ActionResult<ApiResponse<bool>>> Save([FromBody] WorkstationDto dto)
    {
        var result = await _workstationService.SaveAsync(dto);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    /// <summary>
    /// 删除
    /// </summary>
    [HttpPost("delete/{id}")]
    [Authorize(Roles = Roles.Policies.ScanDelete)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _workstationService.DeleteAsync(id);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    // ========== 打印 ==========

    [HttpPost("print-batch-file")]
    [Authorize(Roles = Roles.Policies.ScanView)]
    public async Task<IActionResult> PrintBatchFile([FromBody] WorkstationPrintBatchRequest request)
    {
        if (request.Ids.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("请至少选择一条记录"));
        var pdfBytes = await _workstationService.PrintBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "工位信息-选中.pdf");
    }

}
