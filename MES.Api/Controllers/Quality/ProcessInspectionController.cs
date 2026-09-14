using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.Interfaces.Quality;

namespace MES.Api.Controllers.Quality;

/// <summary>
/// 过程检验控制器
/// </summary>
[ApiController]
[Route("api/process-inspection")]
[Authorize]
public class ProcessInspectionController : ControllerBase
{
    private readonly IProcessInspectionService _service;

    public ProcessInspectionController(IProcessInspectionService service)
    {
        _service = service;
    }

    /// <summary>
    /// 跨批次查询所有过程检验记录（分页）
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ProcessInspectionDto>>>> GetAll(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] DateTime? inspectionDateFrom = null,
        [FromQuery] DateTime? inspectionDateTo = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = sortBy ?? "createdtime",
            IsDescending = isDescending,
            InspectionDateFrom = inspectionDateFrom,
            InspectionDateTo = inspectionDateTo
        };
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetAllAsync(query);
        return Ok(ApiResponse<PagedResult<ProcessInspectionDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    /// <summary>
    /// 批量创建过程检验记录
    /// </summary>
    [HttpPost("batch")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<ProcessInspectionDto>>>> BatchCreate(
        [FromBody] List<CreateProcessInspectionRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<ProcessInspectionDto>>.Fail("请求参数无效"));
        if (requests.Count == 0)
            return BadRequest(ApiResponse<List<ProcessInspectionDto>>.Fail("请求列表不能为空"));
        var result = await _service.BatchCreateAsync(requests);
        return Ok(ApiResponse<List<ProcessInspectionDto>>.Ok(result, "批量创建成功"));
    }

    /// <summary>
    /// 更新过程检验记录
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse<ProcessInspectionDto>>> Update(
        int id, [FromBody] UpdateProcessInspectionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<ProcessInspectionDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<ProcessInspectionDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除过程检验记录
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.QualityDelete)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    /// <summary>批量打印选中记录（PDF 文件）</summary>
    [HttpPost("print-batch-file")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<IActionResult> PrintBatchFile([FromBody] ProcessInspectionPrintBatchRequest request)
    {
        if (request.Ids.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("请至少选择一条记录"));
        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        return File(pdfBytes, "application/pdf", "过程检验-选中.pdf");
    }

    /// <summary>单据式打印选中记录（A4 竖版每条一页，含检验照片）</summary>
    [HttpPost("print-selected-doc-file")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<IActionResult> PrintSelectedDocFile([FromBody] ProcessInspectionPrintBatchRequest request)
    {
        if (request.Ids.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("请至少选择一条记录"));
        var pdfBytes = await _service.PrintSelectedDocAsync(request.Ids);
        return File(pdfBytes, "application/pdf", $"过程检验记录_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
    }

    // ========== 照片附件 ==========

    /// <summary>查询某条记录的照片列表</summary>
    [HttpGet("{id}/attachments")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<List<ProcessInspectionAttachmentDto>>>> GetAttachments(int id)
    {
        var result = await _service.GetAttachmentsAsync(id);
        return Ok(ApiResponse<List<ProcessInspectionAttachmentDto>>.Ok(result));
    }

    /// <summary>上传照片（单条上限 3 张，超限返回业务错误）</summary>
    [HttpPost("{id}/attachments")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ProcessInspectionAttachmentDto>>> UploadAttachment(
        int id, [FromForm] IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<ProcessInspectionAttachmentDto>.Fail("请选择要上传的图片"));

        await using var stream = file.OpenReadStream();
        var result = await _service.AddAttachmentAsync(id, stream, file.FileName, file.ContentType);
        return Ok(ApiResponse<ProcessInspectionAttachmentDto>.Ok(result, "上传成功"));
    }

    /// <summary>读取照片内容</summary>
    [HttpGet("{id}/attachments/{attachmentId}/file")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<IActionResult> GetAttachmentFile(int id, int attachmentId)
    {
        var content = await _service.GetAttachmentContentAsync(id, attachmentId);
        if (content == null) return NotFound();
        return File(content.Content, content.ContentType, enableRangeProcessing: true);
    }

    /// <summary>删除照片（同时删除磁盘文件）</summary>
    [HttpDelete("{id}/attachments/{attachmentId}")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse>> DeleteAttachment(int id, int attachmentId)
    {
        await _service.DeleteAttachmentAsync(id, attachmentId);
        return Ok(ApiResponse.Ok("照片已删除"));
    }
}
