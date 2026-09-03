using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs.Payroll;
using MES.Core.Interfaces.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Payroll;

/// <summary>
/// 「津贴与处罚」接口 — 月度金额录入表（宽表固定 9 列，每人每月一行），按月读取网格 + 整月保存
/// </summary>
[ApiController]
[Route("api/payroll-allowance")]
[Authorize]
public class PayrollAllowanceController : ControllerBase
{
    private readonly IPayrollAllowanceService _allowanceService;

    public PayrollAllowanceController(IPayrollAllowanceService allowanceService)
    {
        _allowanceService = allowanceService;
    }

    /// <summary>按月读取津贴网格（IsActive 在册员工 ∪ 当月已有记录员工，按工号升序）</summary>
    [HttpGet("month")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<AllowanceMonthDto>>> GetMonth(
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var result = await _allowanceService.GetMonthAsync(year, month);
        return Ok(ApiResponse<AllowanceMonthDto>.Ok(result));
    }

    /// <summary>整月保存津贴（每人每月一行 upsert；Rows 空 = 清空整月），返回已保存员工行数</summary>
    [HttpPost("month/save")]
    [Authorize(Roles = Roles.Policies.SalaryEdit)]
    public async Task<ActionResult<ApiResponse<int>>> SaveMonth([FromBody] SaveAllowanceMonthDto input)
    {
        var saved = await _allowanceService.SaveMonthAsync(input.Year, input.Month, input.Rows);
        return Ok(ApiResponse<int>.Ok(saved));
    }
}
