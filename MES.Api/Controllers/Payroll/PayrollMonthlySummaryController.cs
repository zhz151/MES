using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs.Payroll;
using MES.Core.Interfaces.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Payroll;

/// <summary>
/// 月工资汇总接口 — 员工某结算月完整应发/实发（各子页已保存金额 + 考勤派生），实时重算展示 + 整月保存快照 + 两种打印
/// </summary>
[ApiController]
[Route("api/payroll-monthly-summary")]
[Authorize]
public class PayrollMonthlySummaryController : ControllerBase
{
    private readonly IPayrollMonthlySummaryService _summaryService;

    public PayrollMonthlySummaryController(IPayrollMonthlySummaryService summaryService)
    {
        _summaryService = summaryService;
    }

    /// <summary>按月读取工资汇总（实时重算展示；HasSaved = 本月是否已保存快照）</summary>
    [HttpGet("month")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<MonthlySummaryMonthDto>>> GetMonth(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] string? keyword)
    {
        var result = await _summaryService.GetMonthAsync(year, month, keyword);
        return Ok(ApiResponse<MonthlySummaryMonthDto>.Ok(result));
    }

    /// <summary>整月保存：按派生口径重算整月并替换快照（每人每月一行），返回已保存行数</summary>
    [HttpPost("save")]
    [Authorize(Roles = Roles.Policies.SalaryEdit)]
    public async Task<ActionResult<ApiResponse<int>>> SaveMonth([FromBody] SaveMonthlySummaryDto input)
    {
        var saved = await _summaryService.SaveMonthAsync(input.Year, input.Month);
        return Ok(ApiResponse<int>.Ok(saved, $"已保存本月工资津贴汇总 {saved} 人"));
    }

    /// <summary>全部打印：一张 A4 横向整表（读已保存快照；未保存抛业务异常提示先保存）</summary>
    [HttpPost("print-all-file")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<IActionResult> PrintAllFile([FromBody] SaveMonthlySummaryDto input)
    {
        var pdfBytes = await _summaryService.PrintAllAsync(input.Year, input.Month);
        return File(pdfBytes, "application/pdf", $"{input.Year}年{input.Month}月工资津贴汇总表.pdf");
    }

    /// <summary>个人打印：每人一条带表头的两行带，便于裁剪发放（读已保存快照；未保存抛业务异常提示先保存）</summary>
    [HttpPost("print-personal-file")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<IActionResult> PrintPersonalFile([FromBody] SaveMonthlySummaryDto input)
    {
        var pdfBytes = await _summaryService.PrintPersonalAsync(input.Year, input.Month);
        return File(pdfBytes, "application/pdf", $"{input.Year}年{input.Month}月工资条.pdf");
    }
}
