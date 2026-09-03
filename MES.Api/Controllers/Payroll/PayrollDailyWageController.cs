using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Interfaces.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Payroll;

/// <summary>
/// 每日工资接口 — 非计件工资 / 个人计件工资 两表按月读取与整月保存
/// </summary>
[ApiController]
[Route("api/payroll-daily-wage")]
[Authorize]
public class PayrollDailyWageController : ControllerBase
{
    private readonly IPayrollDailyWageService _dailyWageService;

    public PayrollDailyWageController(IPayrollDailyWageService dailyWageService)
    {
        _dailyWageService = dailyWageService;
    }

    /// <summary>按月获取该组员工 + 逐日已保存/引擎草稿（月视图网格数据）；group: non-piece / piece</summary>
    [HttpGet("month")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<DailyWageMonthDto>>> GetMonth(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] string? group,
        [FromQuery] string? keyword = null)
    {
        var groupEnum = PayrollWageGroups.ParseKey(group);
        var result = await _dailyWageService.GetMonthAsync(year, month, groupEnum, keyword);
        return Ok(ApiResponse<DailyWageMonthDto>.Ok(result));
    }

    /// <summary>整月保存（有值 upsert 快照落库，清空删除）</summary>
    [HttpPost("save")]
    [Authorize(Roles = Roles.Policies.SalaryEdit)]
    public async Task<ActionResult<ApiResponse<int>>> Save([FromBody] SaveDailyWageDto request)
    {
        var count = await _dailyWageService.SaveMonthAsync(request);
        return Ok(ApiResponse<int>.Ok(count));
    }
}
