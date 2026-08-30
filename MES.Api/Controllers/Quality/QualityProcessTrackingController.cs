using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.Interfaces.Quality;

namespace MES.Api.Controllers.Quality;

/// <summary>
/// 质量过程跟踪控制器（只读读模型）
/// </summary>
[ApiController]
[Route("api/quality-process-tracking")]
[Authorize]
public class QualityProcessTrackingController : ControllerBase
{
    private readonly IQualityProcessTrackingService _service;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public QualityProcessTrackingController(IQualityProcessTrackingService service)
    {
        _service = service;
    }

    /// <summary>分页查询质量过程跟踪数据</summary>
    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<PagedResult<QualityProcessTrackingDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null,
        [FromQuery] DateTime? receiveDateFrom = null,
        [FromQuery] DateTime? receiveDateTo = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        QueryParams query = new() { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword, SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy, IsDescending = isDescending, ReceiveDateFrom = receiveDateFrom, ReceiveDateTo = receiveDateTo };
        if (!string.IsNullOrEmpty(filters))
        {
            var f = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, _jsonOptions);
            if (f != null && f.Count > 0)
                query.Filters = f;
        }

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PagedResult<QualityProcessTrackingDto>>.Fail("参数无效"));

        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<QualityProcessTrackingDto>>.Ok(result));
    }

    /// <summary>获取筛选上下文（各列去重值）</summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    /// <summary>批量打印选中记录（PDF 文件）</summary>
    [HttpPost("print-batch-file")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<IActionResult> PrintBatchFile([FromBody] QualityProcessTrackingPrintBatchRequest request)
    {
        if (request.Ids.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("请至少选择一条记录"));

        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "成检追踪-选中.pdf");
    }

    /// <summary>全量刷新所有物化行（聚合口径/唯一键变更后的存量重算）</summary>
    [HttpPost("refresh-all")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse<int>>> RefreshAll()
    {
        await _service.RefreshAllAsync();
        return Ok(ApiResponse<int>.Ok(0));
    }
}
