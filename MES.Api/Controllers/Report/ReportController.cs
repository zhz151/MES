using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Report;
using MES.Services.Report;
using MES.Services.Printing;

namespace MES.Api.Controllers.Report;

/// <summary>
/// 报表控制器 — 只读聚合查询
/// </summary>
[ApiController]
[Route("api/report")]
[Authorize]
public class ReportController : ControllerBase
{
    private readonly ReportService _reportService;
    private readonly ILogger<ReportController> _logger;

    public ReportController(ReportService reportService, ILogger<ReportController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// 获取产量报表 — 日期范围的日产量聚合透视表
    /// </summary>
    [HttpGet("daily-output")]
    public async Task<ActionResult<ApiResponse<DailyProductionReportResponse>>> GetDailyProductionReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate)
    {
        if (fromDate > toDate)
            return BadRequest(ApiResponse<DailyProductionReportResponse>.Fail("起始日期不能晚于结束日期"));

        if ((toDate - fromDate).TotalDays > 366)
            return BadRequest(ApiResponse<DailyProductionReportResponse>.Fail("查询范围不能超过366天"));

        var result = await _reportService.GetDailyProductionReportAsync(fromDate, toDate);
        return Ok(ApiResponse<DailyProductionReportResponse>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 产量报表打印 — 生成 PDF 并直接返回文件流
    /// </summary>
    [HttpPost("daily-output/print-file")]
    public async Task<IActionResult> PrintDailyProductionReport(
        [FromBody] DailyProductionReportPrintRequest request)
    {
        if (!DateTime.TryParse(request.FromDate, out var fromDate) ||
            !DateTime.TryParse(request.ToDate, out var toDate))
        {
            return BadRequest("无效的日期格式");
        }

        if (fromDate > toDate)
            return BadRequest("起始日期不能晚于结束日期");

        var report = await _reportService.GetDailyProductionReportAsync(fromDate, toDate);
        if (report.Rows.Count == 0)
            return BadRequest("选定日期范围内暂无数据");

        var visibleColumnKeys = request.Columns?.Select(c => c.Key).ToList();
        var title = $"产量报表（{request.FromDate} ~ {request.ToDate}）";
        var pdfBytes = ReportPrintHelper.GenerateProductionReportPdf(title, report, visibleColumnKeys);

        return File(pdfBytes, "application/pdf", "production_report.pdf");
    }
}
