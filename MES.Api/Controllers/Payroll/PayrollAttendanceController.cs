using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs.Payroll;
using MES.Core.Interfaces.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Payroll;

/// <summary>
/// 靠工计件月结接口 — 靠工计件工资按月结算（平均小时工资 × 本人出勤 × 靠工系数）的读写
/// </summary>
[ApiController]
[Route("api/payroll-attendance")]
[Authorize]
public class PayrollAttendanceController : ControllerBase
{
    private readonly IPayrollAttendanceService _attendanceService;

    public PayrollAttendanceController(IPayrollAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    /// <summary>按月获取靠工员工结算行（靠工岗位/出勤/系数/合并平均小时工资/引擎草稿 + 已保存金额）</summary>
    [HttpGet("month")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<AttendanceWageMonthDto>>> GetMonth(
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var result = await _attendanceService.GetMonthAsync(year, month);
        return Ok(ApiResponse<AttendanceWageMonthDto>.Ok(result));
    }

    /// <summary>整月月结保存（金额 &gt;0 upsert 快照落库，空/0 删除）</summary>
    [HttpPost("month/save")]
    [Authorize(Roles = Roles.Policies.SalaryEdit)]
    public async Task<ActionResult<ApiResponse<int>>> SaveMonth([FromBody] SaveAttendanceWageDto request)
    {
        var count = await _attendanceService.SaveMonthAsync(request);
        return Ok(ApiResponse<int>.Ok(count));
    }
}
