using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Interfaces.Payroll;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Payroll;

/// <summary>
/// 集体计件月结接口 — 集体计件按月结算（集体=岗位 × 月度评分）与月度评分读写
/// </summary>
[ApiController]
[Route("api/payroll-collective")]
[Authorize]
public class PayrollCollectiveController : ControllerBase
{
    private readonly IPayrollCollectiveService _collectiveService;

    public PayrollCollectiveController(IPayrollCollectiveService collectiveService)
    {
        _collectiveService = collectiveService;
    }

    /// <summary>按月获取各岗位集体结算卡片（岗位池 + 成员出勤/分值/权重/引擎草稿 + 已保存金额）</summary>
    [HttpGet("month")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<CollectiveMonthDto>>> GetMonth(
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var result = await _collectiveService.GetMonthAsync(year, month);
        return Ok(ApiResponse<CollectiveMonthDto>.Ok(result));
    }

    /// <summary>整月月结保存（金额 &gt;0 upsert 快照落库，空/0 删除）</summary>
    [HttpPost("month/save")]
    [Authorize(Roles = Roles.Policies.SalaryEdit)]
    public async Task<ActionResult<ApiResponse<int>>> SaveMonth([FromBody] SaveCollectiveMonthDto request)
    {
        var count = await _collectiveService.SaveMonthAsync(request);
        return Ok(ApiResponse<int>.Ok(count));
    }

    /// <summary>按月获取评分员工集与各自分值</summary>
    [HttpGet("scores")]
    [Authorize(Roles = Roles.Policies.SalaryView)]
    public async Task<ActionResult<ApiResponse<CollectiveScoresDto>>> GetScores(
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var result = await _collectiveService.GetScoresAsync(year, month);
        return Ok(ApiResponse<CollectiveScoresDto>.Ok(result));
    }

    /// <summary>整月评分保存（1–10 upsert，null 删除）</summary>
    [HttpPost("scores/save")]
    [Authorize(Roles = Roles.Policies.SalaryEdit)]
    public async Task<ActionResult<ApiResponse<int>>> SaveScores([FromBody] SaveCollectiveScoresDto request)
    {
        var count = await _collectiveService.SaveScoresAsync(request);
        return Ok(ApiResponse<int>.Ok(count));
    }
}
