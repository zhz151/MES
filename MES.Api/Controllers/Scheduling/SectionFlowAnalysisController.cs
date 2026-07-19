using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Scheduling;
using MES.Core.Interfaces.Scheduling;

namespace MES.Api.Controllers.Scheduling;

[ApiController]
[Route("api/section-flow-analysis")]
[Authorize]
public class SectionFlowAnalysisController : ControllerBase
{
    private readonly ISectionFlowAnalysisService _service;

    public SectionFlowAnalysisController(ISectionFlowAnalysisService service)
    {
        _service = service;
    }

    /// <summary>获取生产段流转量分析数据</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<SectionFlowAnalysisDto>>>> GetAnalysis()
    {
        var result = await _service.GetAnalysisAsync();
        return Ok(ApiResponse<List<SectionFlowAnalysisDto>>.Ok(result));
    }

    [HttpPost("print-file")]
    public async Task<IActionResult> PrintFile([FromBody] SectionFlowAnalysisPrintRequest request)
    {
        var pdfBytes = await _service.PrintFileAsync(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "工段流转分析.pdf");
    }

    /// <summary>更新段落分类设置</summary>
    [HttpPut("setting")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> UpdateSetting([FromBody] SectionFlowSettingUpdateDto dto)
    {
        var success = await _service.UpdateSettingAsync(dto);
        if (!success)
            return NotFound(ApiResponse.Fail("段落分类不存在"));
        return Ok(ApiResponse.Ok("保存成功"));
    }
}
