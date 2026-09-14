using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs.Quality;
using MES.Core.Interfaces.Quality;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Quality;

/// <summary>
/// 不合格反馈单控制器 — 只登记问题（含问题照片），处置由不合格报告决定
/// </summary>
[Route("api/nonconforming-feedback")]
[ApiController]
[Authorize]
public class NonconformingFeedbackController : ControllerBase
{
    private readonly INonconformingFeedbackService _service;

    public NonconformingFeedbackController(INonconformingFeedbackService service)
    {
        _service = service;
    }

    /// <summary>分页查询</summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<PagedResult<NonconformingFeedbackDto>>>> GetAll(
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
            SortBy = sortBy ?? "reportdate",
            IsDescending = isDescending,
            ReportDateFrom = reportDateFrom,
            ReportDateTo = reportDateTo
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
        return Ok(ApiResponse<PagedResult<NonconformingFeedbackDto>>.Ok(result));
    }

    /// <summary>获取详情（含附件列表）</summary>
    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<NonconformingFeedbackDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<NonconformingFeedbackDto>.Fail("不合格反馈单不存在"));
        return Ok(ApiResponse<NonconformingFeedbackDto>.Ok(result));
    }

    /// <summary>创建</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse<NonconformingFeedbackDto>>> Create(
        [FromBody] CreateNonconformingFeedbackRequest request)
    {
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<NonconformingFeedbackDto>.Ok(result, "创建成功"));
    }

    /// <summary>更新</summary>
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse<NonconformingFeedbackDto>>> Update(
        int id, [FromBody] UpdateNonconformingFeedbackRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<NonconformingFeedbackDto>.Ok(result, "更新成功"));
    }

    /// <summary>删除</summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.QualityDelete)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    /// <summary>各列去重值，用于 ExcelFilter 下拉</summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    /// <summary>按生产编号调取批次信息（工单号/工厂牌号）</summary>
    [HttpGet("lookup-batch")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<NonconformingFeedbackLookupResultDto?>>> LookupBatch(
        [FromQuery] string batchNo)
    {
        var result = await _service.LookupBatchAsync(batchNo);
        return Ok(ApiResponse<NonconformingFeedbackLookupResultDto?>.Ok(result));
    }

    // ========== 附件 ==========

    /// <summary>查询附件列表</summary>
    [HttpGet("{id}/attachments")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<List<NonconformingFeedbackAttachmentDto>>>> GetAttachments(int id)
    {
        var result = await _service.GetAttachmentsAsync(id);
        return Ok(ApiResponse<List<NonconformingFeedbackAttachmentDto>>.Ok(result));
    }

    /// <summary>上传附件（登记方与处置方均可）</summary>
    [HttpPost("{id}/attachments")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<NonconformingFeedbackAttachmentDto>>> UploadAttachment(
        int id, [FromForm] IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<NonconformingFeedbackAttachmentDto>.Fail("请选择要上传的图片"));

        await using var stream = file.OpenReadStream();
        var result = await _service.AddAttachmentAsync(id, stream, file.FileName, file.ContentType);
        return Ok(ApiResponse<NonconformingFeedbackAttachmentDto>.Ok(result, "上传成功"));
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

    /// <summary>打印单条不合格反馈单（生成 PDF，含问题照片）</summary>
    [HttpPost("{id}/print-file")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<IActionResult> PrintFile(int id)
    {
        var pdf = await _service.GetPrintPdfAsync(id);
        return File(pdf, "application/pdf", $"不合格反馈单_FB{id:D4}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
    }
}
