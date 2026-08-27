using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.Helpers;
using MES.Core.DTOs.Equipment;
using MES.Core.Interfaces.Equipment;

namespace MES.Api.Controllers.Equipment;

[ApiController]
[Route("api/equipment")]
[Authorize]
public class EquipmentController : ControllerBase
{
    private readonly IEquipmentService _service;

    public EquipmentController(IEquipmentService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<ActionResult<ApiResponse<PagedResult<EquipmentListDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? lifecycleStatus = null,
        [FromQuery] string? usageType = null,
        [FromQuery] string? runningStatus = null,
        [FromQuery] string? inspectionStatus = null,
        [FromQuery] string? maintStatus = null,
        [FromQuery] string? location = null,
        [FromQuery] string? relatedSection = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new EquipmentQueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy,
            IsDescending = isDescending,
            LifecycleStatus = EnumHelper.TryParse<MES.Core.Enums.LifecycleStatus>(lifecycleStatus),
            UsageType = EnumHelper.TryParse<MES.Core.Enums.UsageType>(usageType),
            RunningStatus = EnumHelper.TryParse<MES.Core.Enums.RunningStatus>(runningStatus),
            InspectionStatus = EnumHelper.TryParse<MES.Core.Enums.EquipmentTaskStatus>(inspectionStatus),
            MaintStatus = EnumHelper.TryParse<MES.Core.Enums.EquipmentTaskStatus>(maintStatus),
            Location = location,
            RelatedSection = relatedSection
        };
        if (!string.IsNullOrEmpty(filters))
        {
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<EquipmentListDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("all-list")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<ActionResult<ApiResponse<List<EquipmentListDto>>>> GetAllList()
    {
        var result = await _service.GetAllListAsync();
        return Ok(ApiResponse<List<EquipmentListDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<ActionResult<ApiResponse<List<EquipmentListDto>>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<EquipmentListDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<ActionResult<ApiResponse<EquipmentDetailDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<EquipmentDetailDto>.Ok(result, "查询成功"));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Policies.EquipmentEdit)]
    public async Task<ActionResult<ApiResponse<EquipmentDetailDto>>> Create([FromBody] CreateEquipmentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<EquipmentDetailDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<EquipmentDetailDto>.Ok(result, "创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.EquipmentEdit)]
    public async Task<ActionResult<ApiResponse<EquipmentDetailDto>>> Update(int id, [FromBody] UpdateEquipmentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<EquipmentDetailDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<EquipmentDetailDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.EquipmentDelete)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

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
    /// 批量打印设备台账（直接返回 PDF 文件）
    /// </summary>
    [HttpPost("print-batch-file")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<IActionResult> PrintBatchFile([FromBody] EquipmentPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "设备台账打印.pdf");
    }

    [HttpPost("print-all-file")]
    [Authorize(Roles = Roles.Policies.EquipmentView)]
    public async Task<IActionResult> PrintAllFile([FromBody] EquipmentPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var query = new EquipmentQueryParams
        {
            Keyword = request.Keyword,
            SortBy = string.IsNullOrEmpty(request.SortBy) ? "CreatedTime" : request.SortBy,
            IsDescending = request.IsDescending,
            LifecycleStatus = request.LifecycleStatus,
            UsageType = request.UsageType,
            RunningStatus = request.RunningStatus,
            InspectionStatus = request.InspectionStatus,
            MaintStatus = request.MaintStatus,
            Location = request.Location,
            RelatedSection = request.RelatedSection
        };
        var pdfBytes = await _service.PrintAllAsync(query, request.Columns);
        return File(pdfBytes, "application/pdf", "设备台账列表.pdf");
    }
}
