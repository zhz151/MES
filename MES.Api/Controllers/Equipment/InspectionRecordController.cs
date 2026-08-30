using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Equipment;
using MES.Core.Interfaces.Equipment;

namespace MES.Api.Controllers.Equipment;

/// <summary>
/// 点检记录控制器
/// </summary>
[ApiController]
[Route("api/inspection-record")]
[Authorize]
public class InspectionRecordController : ControllerBase
{
    private readonly IInspectionRecordService _service;

    public InspectionRecordController(IInspectionRecordService service)
    {
        _service = service;
    }

    /// <summary>分页查询点检记录</summary>
    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<ActionResult<ApiResponse<PagedResult<InspectionRecordListDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new InspectionRecordQueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "Id" : sortBy,
            IsDescending = isDescending
        };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<InspectionRecordListDto>>.Ok(result, "查询成功"));
    }

    /// <summary>根据 ID 获取点检记录</summary>
    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<ActionResult<ApiResponse<InspectionRecordListDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<InspectionRecordListDto>.Fail("点检记录不存在"));
        return Ok(ApiResponse<InspectionRecordListDto>.Ok(result, "查询成功"));
    }

    /// <summary>创建点检记录</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Policies.EquipmentEdit)]
    public async Task<ActionResult<ApiResponse<InspectionRecordListDto>>> Create([FromBody] CreateInspectionRecordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<InspectionRecordListDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<InspectionRecordListDto>.Ok(result, "创建成功"));
    }

    /// <summary>批量创建点检记录</summary>
    [HttpPost("batch")]
    [Authorize(Roles = Roles.Policies.EquipmentEdit)]
    public async Task<ActionResult<ApiResponse<List<InspectionRecordListDto>>>> BatchCreate([FromBody] List<CreateInspectionRecordRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<InspectionRecordListDto>>.Fail("请求参数无效"));
        if (requests.Count == 0)
            return BadRequest(ApiResponse<List<InspectionRecordListDto>>.Fail("请求列表不能为空"));
        var result = await _service.CreateBatchAsync(requests);
        return Ok(ApiResponse<List<InspectionRecordListDto>>.Ok(result, "批量创建成功"));
    }

    /// <summary>更新点检记录</summary>
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.EquipmentEdit)]
    public async Task<ActionResult<ApiResponse<InspectionRecordListDto>>> Update(int id, [FromBody] UpdateInspectionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<InspectionRecordListDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<InspectionRecordListDto>.Ok(result, "更新成功"));
    }

    /// <summary>删除点检记录</summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.EquipmentDelete)]
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
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 批量打印点检记录（直接返回 PDF 文件）
    /// </summary>
    [HttpPost("print-batch-file")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<IActionResult> PrintBatchFile([FromBody] InspectionRecordPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "点检记录打印.pdf");
    }

}
