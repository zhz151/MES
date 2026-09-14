using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.Interfaces.Quality;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Quality;

/// <summary>
/// 巡检单控制器 — 质量检验的一种类型（过程巡检记录，含巡检明细与整改闭环）
/// </summary>
[Route("api/inspection-patrol")]
[ApiController]
[Authorize]
public class InspectionPatrolController : ControllerBase
{
    private readonly IInspectionPatrolService _service;

    public InspectionPatrolController(IInspectionPatrolService service)
    {
        _service = service;
    }

    /// <summary>分页查询</summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<PagedResult<InspectionPatrolDto>>>> GetAll(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null,
        [FromQuery] DateTime? patrolDateFrom = null,
        [FromQuery] DateTime? patrolDateTo = null)
    {
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = sortBy ?? "patroldate",
            IsDescending = isDescending,
            ReportDateFrom = patrolDateFrom,
            ReportDateTo = patrolDateTo
        };
        if (!string.IsNullOrEmpty(filters))
        {
            try
            {
                query.Filters = System.Text.Json.JsonSerializer.Deserialize<List<FilterDescriptor>>(
                    filters, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { }
        }
        var result = await _service.GetAllAsync(query);
        return Ok(ApiResponse<PagedResult<InspectionPatrolDto>>.Ok(result));
    }

    /// <summary>获取详情（含巡检明细与附件列表）</summary>
    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<InspectionPatrolDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<InspectionPatrolDto>.Fail("巡检单不存在"));
        return Ok(ApiResponse<InspectionPatrolDto>.Ok(result));
    }

    /// <summary>创建</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse<InspectionPatrolDto>>> Create(
        [FromBody] CreateInspectionPatrolRequest request)
    {
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<InspectionPatrolDto>.Ok(result, "创建成功"));
    }

    /// <summary>更新</summary>
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse<InspectionPatrolDto>>> Update(
        int id, [FromBody] UpdateInspectionPatrolRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<InspectionPatrolDto>.Ok(result, "更新成功"));
    }

    /// <summary>删除</summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.QualityDelete)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    /// <summary>切换「是否闭环」标记</summary>
    [HttpPut("{id}/closed")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse>> SetClosed(int id, [FromQuery] bool isClosed)
    {
        await _service.SetClosedAsync(id, isClosed);
        return Ok(ApiResponse.Ok(isClosed ? "已标记为闭环" : "已取消闭环标记"));
    }

    /// <summary>各列去重值，用于 ExcelFilter 下拉</summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    /// <summary>按生产编号调取批次信息（工单号/工厂牌号/工序组）</summary>
    [HttpGet("lookup-batch")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<InspectionPatrolLookupResultDto?>>> LookupBatch(
        [FromQuery] string batchNo)
    {
        var result = await _service.LookupBatchAsync(batchNo);
        return Ok(ApiResponse<InspectionPatrolLookupResultDto?>.Ok(result));
    }

    /// <summary>在产单位·车间 候选（委外单位档案驱动；在产设备名为纯手输、在产操作人走员工档案，均不经此端点）</summary>
    [HttpGet("position-options")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<InspectionPatrolPositionOptionsDto>>> GetPositionOptions()
    {
        var result = await _service.GetPositionOptionsAsync();
        return Ok(ApiResponse<InspectionPatrolPositionOptionsDto>.Ok(result));
    }

    // ========== 附件 ==========

    /// <summary>查询附件列表</summary>
    [HttpGet("{id}/attachments")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<List<InspectionPatrolAttachmentDto>>>> GetAttachments(int id)
    {
        var result = await _service.GetAttachmentsAsync(id);
        return Ok(ApiResponse<List<InspectionPatrolAttachmentDto>>.Ok(result));
    }

    /// <summary>上传附件（photoType: Patrol=巡检照片 / Rectification=整改验证照片）</summary>
    [HttpPost("{id}/attachments")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<InspectionPatrolAttachmentDto>>> UploadAttachment(
        int id, [FromForm] string photoType, [FromForm] IFormFile? file)
    {
        if (!InspectionPatrolPhotoTypes.IsValid(photoType))
            return BadRequest(ApiResponse<InspectionPatrolAttachmentDto>.Fail("照片类型无效"));
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<InspectionPatrolAttachmentDto>.Fail("请选择要上传的图片"));

        await using var stream = file.OpenReadStream();
        var result = await _service.AddAttachmentAsync(id, photoType, stream, file.FileName, file.ContentType);
        return Ok(ApiResponse<InspectionPatrolAttachmentDto>.Ok(result, "上传成功"));
    }

    /// <summary>读取附件内容</summary>
    [HttpGet("{id}/attachments/{attachmentId}/file")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<IActionResult> GetAttachmentFile(int id, int attachmentId)
    {
        var content = await _service.GetAttachmentContentAsync(id, attachmentId);
        if (content == null) return NotFound();
        return File(content.Content, content.ContentType, enableRangeProcessing: true);
    }

    /// <summary>删除附件（同时删除磁盘文件）</summary>
    [HttpDelete("{id}/attachments/{attachmentId}")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse>> DeleteAttachment(int id, int attachmentId)
    {
        await _service.DeleteAttachmentAsync(id, attachmentId);
        return Ok(ApiResponse.Ok("附件已删除"));
    }

    // ========== 打印 ==========

    /// <summary>打印单条巡检单（生成 PDF，含巡检照片与整改验证照片）</summary>
    [HttpPost("{id}/print-file")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<IActionResult> PrintFile(int id)
    {
        var pdf = await _service.GetPrintPdfAsync(id);
        return File(pdf, "application/pdf", $"巡检单_XJ{id:D4}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
    }
}
