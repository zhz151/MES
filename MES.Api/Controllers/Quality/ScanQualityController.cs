using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.Interfaces.Quality;
using MES.Core.Models;

namespace MES.Api.Controllers.Quality;

/// <summary>
/// 扫码链质量端点 — 「巡检」「不合格反馈」的扫码报工入口。
///
/// <b>仅需登录（[Authorize]），不带质量角色档</b>：这两类记录本就是生产现场反馈，
/// 由现场任意登录员工发起；主数据的查看/编辑仍走 api/inspection-patrol、api/nonconforming-feedback 的质量角色档。
/// 操作人实名由服务端按登录账号解析，客户端无需（也无法）指定。
/// </summary>
[Route("api/scan-quality")]
[ApiController]
[Authorize]
public class ScanQualityController : ControllerBase
{
    private readonly IScanQualityService _service;

    public ScanQualityController(IScanQualityService service)
    {
        _service = service;
    }

    // ========== 通用 ==========

    /// <summary>当前扫码人真实姓名（前端只读展示「巡检人 / 反馈人」）</summary>
    [HttpGet("current-operator")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<string>>> GetCurrentOperator()
    {
        var name = await _service.GetCurrentOperatorNameAsync();
        // 必须显式两参：单参形式在 T=string 时会命中已删除的 message-only 重载，把姓名写进 Message、Data 留空
        return Ok(ApiResponse<string>.Ok(name, "查询成功"));
    }

    // ========== 巡检 ==========

    /// <summary>按「批次 + 工序组 + 工段」定位巡检单（未闭环优先，同状态内新→旧）</summary>
    [HttpGet("inspection-patrol/by-key")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<InspectionPatrolDto>>>> GetPatrolsByKey(
        [FromQuery] string batchNo,
        [FromQuery] int processGroupId,
        [FromQuery] string sectionName)
    {
        if (string.IsNullOrWhiteSpace(batchNo))
            return BadRequest(ApiResponse<List<InspectionPatrolDto>>.Fail("生产编号不能为空"));

        var result = await _service.GetPatrolsByKeyAsync(batchNo, processGroupId, sectionName);
        return Ok(ApiResponse<List<InspectionPatrolDto>>.Ok(result));
    }

    /// <summary>巡检单详情（含巡检明细与照片，供第二次扫码呈现第一次结果）</summary>
    [HttpGet("inspection-patrol/{id:int}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<InspectionPatrolDto>>> GetPatrol(int id)
    {
        var result = await _service.GetPatrolAsync(id);
        if (result == null)
            return NotFound(ApiResponse<InspectionPatrolDto>.Fail("巡检单不存在"));
        return Ok(ApiResponse<InspectionPatrolDto>.Ok(result));
    }

    /// <summary>按生产编号调取批次信息（工单号/工厂牌号/工序组与工段）</summary>
    [HttpGet("inspection-patrol/lookup-batch")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<InspectionPatrolLookupResultDto?>>> LookupPatrolBatch(
        [FromQuery] string batchNo)
    {
        var result = await _service.LookupPatrolBatchAsync(batchNo);
        return Ok(ApiResponse<InspectionPatrolLookupResultDto?>.Ok(result));
    }

    /// <summary>在产单位/车间 候选（委外单位档案）</summary>
    [HttpGet("inspection-patrol/position-options")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<InspectionPatrolPositionOptionsDto>>> GetPatrolPositionOptions()
    {
        var result = await _service.GetPatrolPositionOptionsAsync();
        return Ok(ApiResponse<InspectionPatrolPositionOptionsDto>.Ok(result));
    }

    /// <summary>扫码新建巡检单（巡检人 = 当前登录人）</summary>
    [HttpPost("inspection-patrol")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<InspectionPatrolDto>>> CreatePatrol(
        [FromBody] CreateInspectionPatrolRequest request)
    {
        var result = await _service.CreatePatrolAsync(request);
        return Ok(ApiResponse<InspectionPatrolDto>.Ok(result, "巡检单已提交"));
    }

    /// <summary>扫码整改回填（整改人 = 当前登录人；不动巡检明细）</summary>
    [HttpPost("inspection-patrol/{id:int}/rectify")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<InspectionPatrolDto>>> RectifyPatrol(
        int id, [FromBody] InspectionPatrolRectifyRequest request)
    {
        var result = await _service.RectifyPatrolAsync(id, request);
        return Ok(ApiResponse<InspectionPatrolDto>.Ok(result, "整改回填成功"));
    }

    // ---------- 巡检照片 ----------

    /// <summary>上传巡检照片（photoType: Patrol=巡检照片 / Rectification=整改验证照片）</summary>
    [HttpPost("inspection-patrol/{id:int}/attachments")]
    [Authorize]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<InspectionPatrolAttachmentDto>>> UploadPatrolAttachment(
        int id, [FromForm] string photoType, [FromForm] IFormFile? file)
    {
        if (!InspectionPatrolPhotoTypes.IsValid(photoType))
            return BadRequest(ApiResponse<InspectionPatrolAttachmentDto>.Fail("照片类型无效"));
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<InspectionPatrolAttachmentDto>.Fail("请选择要上传的图片"));

        await using var stream = file.OpenReadStream();
        var result = await _service.AddPatrolAttachmentAsync(id, photoType, stream, file.FileName, file.ContentType);
        return Ok(ApiResponse<InspectionPatrolAttachmentDto>.Ok(result, "上传成功"));
    }

    /// <summary>读取巡检照片</summary>
    [HttpGet("inspection-patrol/{id:int}/attachments/{attachmentId:int}/file")]
    [Authorize]
    public async Task<IActionResult> GetPatrolAttachmentFile(int id, int attachmentId)
    {
        var content = await _service.GetPatrolAttachmentContentAsync(id, attachmentId);
        if (content == null) return NotFound();
        return File(content.Content, content.ContentType, enableRangeProcessing: true);
    }

    /// <summary>删除巡检照片</summary>
    [HttpDelete("inspection-patrol/{id:int}/attachments/{attachmentId:int}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse>> DeletePatrolAttachment(int id, int attachmentId)
    {
        await _service.DeletePatrolAttachmentAsync(id, attachmentId);
        return Ok(ApiResponse.Ok("照片已删除"));
    }

    // ========== 不合格反馈 ==========

    /// <summary>按生产编号调取批次信息（工单号/工厂牌号/工序组与工段）</summary>
    [HttpGet("nonconforming-feedback/lookup-batch")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<NonconformingFeedbackLookupResultDto?>>> LookupFeedbackBatch(
        [FromQuery] string batchNo)
    {
        var result = await _service.LookupFeedbackBatchAsync(batchNo);
        return Ok(ApiResponse<NonconformingFeedbackLookupResultDto?>.Ok(result));
    }

    /// <summary>扫码新建不合格反馈单（反馈人 = 当前登录人）</summary>
    [HttpPost("nonconforming-feedback")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<NonconformingFeedbackDto>>> CreateFeedback(
        [FromBody] CreateNonconformingFeedbackRequest request)
    {
        var result = await _service.CreateFeedbackAsync(request);
        return Ok(ApiResponse<NonconformingFeedbackDto>.Ok(result, "不合格反馈已提交"));
    }

    /// <summary>上传问题照片</summary>
    [HttpPost("nonconforming-feedback/{id:int}/attachments")]
    [Authorize]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<NonconformingFeedbackAttachmentDto>>> UploadFeedbackAttachment(
        int id, [FromForm] IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<NonconformingFeedbackAttachmentDto>.Fail("请选择要上传的图片"));

        await using var stream = file.OpenReadStream();
        var result = await _service.AddFeedbackAttachmentAsync(id, stream, file.FileName, file.ContentType);
        return Ok(ApiResponse<NonconformingFeedbackAttachmentDto>.Ok(result, "上传成功"));
    }

    /// <summary>读取问题照片</summary>
    [HttpGet("nonconforming-feedback/{id:int}/attachments/{attachmentId:int}/file")]
    [Authorize]
    public async Task<IActionResult> GetFeedbackAttachmentFile(int id, int attachmentId)
    {
        var content = await _service.GetFeedbackAttachmentContentAsync(id, attachmentId);
        if (content == null) return NotFound();
        return File(content.Content, content.ContentType, enableRangeProcessing: true);
    }

    /// <summary>删除问题照片</summary>
    [HttpDelete("nonconforming-feedback/{id:int}/attachments/{attachmentId:int}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse>> DeleteFeedbackAttachment(int id, int attachmentId)
    {
        await _service.DeleteFeedbackAttachmentAsync(id, attachmentId);
        return Ok(ApiResponse.Ok("照片已删除"));
    }
}
