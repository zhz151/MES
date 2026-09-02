using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs.Payroll;
using MES.Core.Interfaces.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Payroll;

/// <summary>
/// 考勤接口 — 考勤表按月读取与整月保存
/// </summary>
[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    /// <summary>按月获取启用员工 + 当月出勤（月视图网格数据）</summary>
    [HttpGet("month")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<AttendanceMonthDto>>> GetMonth(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] string? keyword = null)
    {
        var result = await _attendanceService.GetMonthAsync(year, month, keyword);
        return Ok(ApiResponse<AttendanceMonthDto>.Ok(result));
    }

    /// <summary>整月保存（有值 upsert，清空删除）</summary>
    [HttpPost("save")]
    [Authorize(Roles = Roles.Policies.SalaryEdit)]
    public async Task<ActionResult<ApiResponse<int>>> Save([FromBody] SaveAttendanceDto request)
    {
        var count = await _attendanceService.SaveMonthAsync(request);
        return Ok(ApiResponse<int>.Ok(count));
    }
}
