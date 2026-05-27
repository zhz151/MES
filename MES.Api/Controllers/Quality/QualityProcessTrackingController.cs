using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

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
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<QualityProcessTrackingDto>>>> GetPaged(
        [FromQuery] QueryParams query,
        [FromQuery] string? filters = null)
    {
        if (!string.IsNullOrEmpty(filters))
        {
            var f = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, _jsonOptions);
            if (f != null && f.Count > 0)
                query.Filters = f;
        }

        // 限制 pageSize 防止攻击
        if (query.PageSize > 5000)
            query.PageSize = 5000;

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PagedResult<QualityProcessTrackingDto>>.Fail("参数无效"));

        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<QualityProcessTrackingDto>>.Ok(result));
    }

    /// <summary>获取筛选上下文（各列去重值）</summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }
}
