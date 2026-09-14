using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Order;
using MES.Core.Interfaces.Batch;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Batch;

/// <summary>
/// 委外单位档案（工段委外单位主档），仿供应商档案 Controller，挂批次域三档权限
/// </summary>
[ApiController]
[Route("api/outsource-vendor")]
[Authorize]
public class OutsourceVendorProfileController : ControllerBase
{
    private readonly IOutsourceVendorProfileService _service;

    public OutsourceVendorProfileController(IOutsourceVendorProfileService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<ActionResult<ApiResponse<PagedResult<OutsourceVendorProfileDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null,
        [FromQuery] DateTime? sendDateFrom = null,
        [FromQuery] DateTime? sendDateTo = null,
        [FromQuery] DateTime? recoveryDateFrom = null,
        [FromQuery] DateTime? recoveryDateTo = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy,
            IsDescending = isDescending,
            VendorSendDateFrom = sendDateFrom,
            VendorSendDateTo = sendDateTo,
            VendorRecoveryDateFrom = recoveryDateFrom,
            VendorRecoveryDateTo = recoveryDateTo
        };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { /* ignore invalid JSON */ }
        }
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<OutsourceVendorProfileDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<ActionResult<ApiResponse<OutsourceVendorProfileDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<OutsourceVendorProfileDto>.Ok(result, "查询成功"));
    }

    [HttpGet("active")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<ActionResult<ApiResponse<List<OutsourceVendorProfileDto>>>> GetActive()
    {
        var result = await _service.GetActiveAsync();
        return Ok(ApiResponse<List<OutsourceVendorProfileDto>>.Ok(result, "查询成功"));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Policies.BatchEdit)]
    public async Task<ActionResult<ApiResponse<OutsourceVendorProfileDto>>> Create([FromBody] CreateOutsourceVendorRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<OutsourceVendorProfileDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<OutsourceVendorProfileDto>.Ok(result, "创建成功"));
    }

    [HttpPost("batch")]
    [Authorize(Roles = Roles.Policies.BatchEdit)]
    public async Task<ActionResult<ApiResponse<List<OutsourceVendorProfileDto>>>> CreateBatch([FromBody] List<CreateOutsourceVendorRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<OutsourceVendorProfileDto>>.Fail("请求参数无效"));
        if (requests == null || requests.Count == 0)
            return BadRequest(ApiResponse<List<OutsourceVendorProfileDto>>.Fail("请求列表不能为空"));
        var result = await _service.CreateBatchAsync(requests);
        return Ok(ApiResponse<List<OutsourceVendorProfileDto>>.Ok(result, "批量创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.BatchEdit)]
    public async Task<ActionResult<ApiResponse<OutsourceVendorProfileDto>>> Update(int id, [FromBody] UpdateOutsourceVendorRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<OutsourceVendorProfileDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<OutsourceVendorProfileDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.BatchDelete)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    // ========== 筛选上下文 ==========

    /// <summary>
    /// 获取筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result, "查询成功"));
    }

    // ========== 打印 ==========

    /// <summary>
    /// 打印选中列表（按当前可见列渲染列表 PDF，Mode A 前端已准备数据，② 往来信息 统计列文本由此带出）
    /// </summary>
    [HttpPost("print-list-file")]
    [Authorize(Roles = Roles.Policies.BatchView)]
    public async Task<IActionResult> PrintListFile([FromBody] OrderPrintListRequest request)
    {
        var pdfBytes = await _service.PrintOutsourceVendorListAsync(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "委外单位列表.pdf");
    }
}
