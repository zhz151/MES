using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

/// <summary>
/// 工段委外控制器（委外发出 + 委外回收）
/// </summary>
[ApiController]
[Route("api/section-outsource")]
[Authorize]
public class SectionOutsourceController : ControllerBase
{
    private readonly ISectionOutsourceService _service;
    private readonly ILogger<SectionOutsourceController> _logger;

    public SectionOutsourceController(ISectionOutsourceService service, ILogger<SectionOutsourceController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // ========== 工段委外 ==========

    /// <summary>
    /// 根据ID列表获取委外记录（用于批量回收）
    /// </summary>
    [HttpGet("by-ids")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<SectionOutsourceDto>>>> GetByIds(
        [FromQuery] string ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
            return BadRequest(ApiResponse<List<SectionOutsourceDto>>.Fail("ids参数不能为空"));
        var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => int.TryParse(id, out var parsed) ? parsed : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();
        var result = await _service.GetByIdsAsync(idList);
        return Ok(ApiResponse<List<SectionOutsourceDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 跨批次分页查询委外发出记录
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<SectionOutsourceDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] DateTime? sendOutDateFrom = null,
        [FromQuery] DateTime? sendOutDateTo = null,
        [FromQuery] DateTime? actualRecoveryDateFrom = null,
        [FromQuery] DateTime? actualRecoveryDateTo = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = sortBy ?? "createdtime",
            IsDescending = isDescending,
            SendOutDateFrom = sendOutDateFrom,
            SendOutDateTo = sendOutDateTo,
            ActualRecoveryDateFrom = actualRecoveryDateFrom,
            ActualRecoveryDateTo = actualRecoveryDateTo
        };
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<SectionOutsourceDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建委外发出
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SectionOutsourceDto>>> Create([FromBody] CreateSectionOutsourceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SectionOutsourceDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<SectionOutsourceDto>.Ok(result, "创建成功"));
    }

    /// <summary>
    /// 批量创建委外发出
    /// </summary>
    [HttpPost("batch")]
    [Authorize(Roles = $"{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<SectionOutsourceDto>>>> BatchCreate(
        [FromBody] List<CreateSectionOutsourceRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<SectionOutsourceDto>>.Fail("请求参数无效"));
        if (requests.Count == 0)
            return BadRequest(ApiResponse<List<SectionOutsourceDto>>.Fail("请求列表不能为空"));
        var result = await _service.BatchCreateAsync(requests);
        return Ok(ApiResponse<List<SectionOutsourceDto>>.Ok(result, "批量创建成功"));
    }

    /// <summary>
    /// 更新委外发出（内联编辑）
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SectionOutsourceDto>>> Update(int id, [FromBody] UpdateSectionOutsourceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SectionOutsourceDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<SectionOutsourceDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除委外发出
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    // ========== 委外回收 ==========

    /// <summary>
    /// 获取指定委外发出的回收明细
    /// </summary>
    [HttpGet("{outsourceId}/recoveries")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<OutsourceRecoveryDto>>>> GetRecoveries(int outsourceId)
    {
        var result = await _service.GetRecoveriesAsync(outsourceId);
        return Ok(ApiResponse<List<OutsourceRecoveryDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 跨批次分页查询回收记录
    /// </summary>
    [HttpGet("recoveries/list")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<OutsourceRecoveryDto>>>> GetRecoveriesPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] DateTime? recoveryDateFrom = null,
        [FromQuery] DateTime? recoveryDateTo = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = sortBy ?? "recoverydate",
            IsDescending = isDescending,
            RecoveryDateFrom = recoveryDateFrom,
            RecoveryDateTo = recoveryDateTo
        };
        var result = await _service.GetRecoveriesPagedAsync(query);
        return Ok(ApiResponse<PagedResult<OutsourceRecoveryDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建委外回收
    /// </summary>
    [HttpPost("recovery")]
    [Authorize(Roles = $"{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<OutsourceRecoveryDto>>> CreateRecovery(
        [FromBody] CreateOutsourceRecoveryRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<OutsourceRecoveryDto>.Fail("请求参数无效"));
        var result = await _service.CreateRecoveryAsync(request);
        return Ok(ApiResponse<OutsourceRecoveryDto>.Ok(result, "创建成功"));
    }

    /// <summary>
    /// 批量创建委外回收
    /// </summary>
    [HttpPost("recoveries/batch")]
    [Authorize(Roles = $"{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<OutsourceRecoveryDto>>>> BatchCreateRecoveries(
        [FromBody] List<CreateOutsourceRecoveryRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<OutsourceRecoveryDto>>.Fail("请求参数无效"));
        if (requests.Count == 0)
            return BadRequest(ApiResponse<List<OutsourceRecoveryDto>>.Fail("请求列表不能为空"));
        var result = await _service.BatchCreateRecoveriesAsync(requests);
        return Ok(ApiResponse<List<OutsourceRecoveryDto>>.Ok(result, "批量创建成功"));
    }

    /// <summary>
    /// 更新委外回收
    /// </summary>
    [HttpPut("recovery/{id}")]
    [Authorize(Roles = $"{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<OutsourceRecoveryDto>>> UpdateRecovery(int id, [FromBody] UpdateOutsourceRecoveryRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<OutsourceRecoveryDto>.Fail("请求参数无效"));
        var result = await _service.UpdateRecoveryAsync(id, request);
        return Ok(ApiResponse<OutsourceRecoveryDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除委外回收
    /// </summary>
    [HttpDelete("recovery/{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse>> DeleteRecovery(int id)
    {
        await _service.DeleteRecoveryAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    // ========== 打印 ==========

    /// <summary>
    /// 批量打印委外发出（选中）
    /// </summary>
    [HttpPost("print-selected")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintSelected([FromBody] SectionOutsourcePrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    /// <summary>
    /// 按筛选条件打印全部委外发出
    /// </summary>
    [HttpPost("print-all")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintAll([FromBody] SectionOutsourcePrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintAllAsync(request.Keyword, request.SortBy, request.IsDescending,
            request.SendOutDateFrom, request.SendOutDateTo,
            request.ActualRecoveryDateFrom, request.ActualRecoveryDateTo,
            request.Columns);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    /// <summary>
    /// 批量打印回收记录（选中）
    /// </summary>
    [HttpPost("recoveries/print-selected")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintRecoverySelected([FromBody] RecoveryPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintRecoveryBatchAsync(request.Ids, request.Columns);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    /// <summary>
    /// 按筛选条件打印全部回收记录
    /// </summary>
    [HttpPost("recoveries/print-all")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintRecoveryAll([FromBody] RecoveryPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintRecoveryAllAsync(request.Keyword, request.SortBy, request.IsDescending,
            request.RecoveryDateFrom, request.RecoveryDateTo, request.Columns);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }
}
