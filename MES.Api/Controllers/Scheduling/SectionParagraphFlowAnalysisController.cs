using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Scheduling;
using MES.Core.Interfaces.Scheduling;

namespace MES.Api.Controllers.Scheduling;

[ApiController]
[Route("api/section-paragraph-flow-analysis")]
[Authorize]
public class SectionParagraphFlowAnalysisController : ControllerBase
{
    private readonly ISectionParagraphFlowAnalysisService _service;

    public SectionParagraphFlowAnalysisController(ISectionParagraphFlowAnalysisService service)
    {
        _service = service;
    }

    /// <summary>获取生产段落流转量分析数据</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Policies.SchedulingView)]
    public async Task<ActionResult<ApiResponse<List<SectionParagraphFlowAnalysisDto>>>> GetAnalysis()
    {
        var result = await _service.GetAnalysisAsync();
        return Ok(ApiResponse<List<SectionParagraphFlowAnalysisDto>>.Ok(result));
    }

    [HttpPost("print-file")]
    [Authorize(Roles = Roles.Policies.SchedulingView)]
    public async Task<IActionResult> PrintFile([FromBody] SectionParagraphFlowAnalysisPrintRequest request)
    {
        var pdfBytes = await _service.PrintFileAsync(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "段落流转分析.pdf");
    }
}
