using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs.Payroll;
using MES.Core.Interfaces.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Payroll;

/// <summary>
/// 杂辅工记录接口 — 登记员工每天做的杂项辅助工作（台账流水，月视图读取 + 逐条新增/编辑/删除）
/// </summary>
[ApiController]
[Route("api/payroll-misc-work")]
[Authorize]
public class PayrollMiscWorkController : ControllerBase
{
    private readonly IPayrollMiscWorkService _miscWorkService;

    public PayrollMiscWorkController(IPayrollMiscWorkService miscWorkService)
    {
        _miscWorkService = miscWorkService;
    }

    /// <summary>按月读取杂辅台账（记录 + 整月条数/总小时/总金额）</summary>
    [HttpGet("month")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<MiscWorkMonthDto>>> GetMonth(
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var result = await _miscWorkService.GetMonthAsync(year, month);
        return Ok(ApiResponse<MiscWorkMonthDto>.Ok(result));
    }

    /// <summary>保存一条杂辅记录（Id=0 新增 / &gt;0 编辑更新，编辑不改员工），返回记录 Id</summary>
    [HttpPost("record")]
    [Authorize(Roles = Roles.Policies.SalaryEdit)]
    public async Task<ActionResult<ApiResponse<int>>> Save([FromBody] MiscWorkRecordInputDto input)
    {
        var id = await _miscWorkService.SaveRecordAsync(input);
        return Ok(ApiResponse<int>.Ok(id));
    }

    /// <summary>删除一条杂辅记录</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Policies.SalaryEdit)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        await _miscWorkService.DeleteRecordAsync(id);
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
