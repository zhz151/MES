using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Quality;

/// <summary>
/// NCR 不合格品报告控制器
/// </summary>
[Route("api/ncr")]
[ApiController]
[Authorize]
public class NcrController : ControllerBase
{
    private readonly INcrService _ncrService;

    public NcrController(INcrService ncrService)
    {
        _ncrService = ncrService;
    }

    /// <summary>分页查询</summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.QualityRead)]
    public async Task<ActionResult<ApiResponse<PagedResult<NcrDto>>>> GetAll(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null,
        [FromQuery] DateTime? reportDateFrom = null,
        [FromQuery] DateTime? reportDateTo = null)
    {
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = sortBy ?? "createdtime",
            IsDescending = isDescending,
            ReportDateFrom = reportDateFrom,
            ReportDateTo = reportDateTo
        };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = System.Text.Json.JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }
        var result = await _ncrService.GetAllAsync(query);
        return Ok(ApiResponse<PagedResult<NcrDto>>.Ok(result));
    }

    /// <summary>获取全部（无分页）</summary>
    [HttpGet("all-list")]
    [Authorize(Roles = Roles.Policies.QualityRead)]
    public async Task<ActionResult<ApiResponse<List<NcrDto>>>> GetAllList()
    {
        var result = await _ncrService.GetAllListAsync();
        return Ok(ApiResponse<List<NcrDto>>.Ok(result));
    }

    /// <summary>获取详情</summary>
    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.QualityRead)]
    public async Task<ActionResult<ApiResponse<NcrDto>>> GetById(int id)
    {
        var result = await _ncrService.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<NcrDto>.Fail("不合格品报告不存在"));
        return Ok(ApiResponse<NcrDto>.Ok(result));
    }

    /// <summary>创建</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Policies.QualityWrite)]
    public async Task<ActionResult<ApiResponse<NcrDto>>> Create([FromBody] CreateNcrRequest request)
    {
        var result = await _ncrService.CreateAsync(request);
        return Ok(ApiResponse<NcrDto>.Ok(result, "创建成功"));
    }

    /// <summary>更新</summary>
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.QualityWrite)]
    public async Task<ActionResult<ApiResponse<NcrDto>>> Update(int id, [FromBody] UpdateNcrRequest request)
    {
        var result = await _ncrService.UpdateAsync(id, request);
        return Ok(ApiResponse<NcrDto>.Ok(result, "更新成功"));
    }

    /// <summary>删除</summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.QualityWrite)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _ncrService.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    /// <summary>状态变更</summary>
    [HttpPut("{id}/status")]
    [Authorize(Roles = Roles.Policies.QualityWrite)]
    public async Task<ActionResult<ApiResponse<NcrDto>>> UpdateStatus(int id, [FromBody] UpdateNcrStatusRequest request)
    {
        var result = await _ncrService.UpdateStatusAsync(id, request);
        return Ok(ApiResponse<NcrDto>.Ok(result, "状态更新成功"));
    }

    /// <summary>根据生产编号调取批次信息</summary>
    [HttpGet("lookup-batch")]
    [Authorize(Roles = Roles.Policies.QualityRead)]
    public async Task<ActionResult<ApiResponse<NcrLookupResultDto?>>> LookupBatch([FromQuery] string batchNo)
    {
        var result = await _ncrService.LookupBatchAsync(batchNo);
        return Ok(ApiResponse<NcrLookupResultDto?>.Ok(result));
    }

    /// <summary>获取待处理批次卡片数据</summary>
    [HttpGet("pending-checks")]
    [Authorize(Roles = Roles.Policies.QualityRead)]
    public async Task<ActionResult<ApiResponse<List<NcrPendingCheckDto>>>> GetPendingChecks()
    {
        var result = await _ncrService.GetPendingChecksAsync();
        return Ok(ApiResponse<List<NcrPendingCheckDto>>.Ok(result));
    }

    /// <summary>获取筛选上下文</summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.QualityRead)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _ncrService.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    /// <summary>打印选中 NCR（生成 HTML）</summary>
    [HttpPost("print-selected-file")]
    [Authorize(Roles = Roles.Policies.QualityWrite)]
    public async Task<ActionResult<ApiResponse<string>>> PrintSelectedFile([FromBody] NcrPrintSelectedRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var html = await _ncrService.PrintSelectedAsync(request.Ids, request.Columns);
        return Ok(ApiResponse<string>.Ok(html));
    }

    /// <summary>打印全部 NCR（生成 HTML）</summary>
    [HttpPost("print-all-file")]
    [Authorize(Roles = Roles.Policies.QualityWrite)]
    public async Task<ActionResult<ApiResponse<string>>> PrintAllFile([FromBody] NcrPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        var html = await _ncrService.PrintAllAsync(request);
        return Ok(ApiResponse<string>.Ok(html));
    }
}
