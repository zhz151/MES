using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

/// <summary>
/// 扫码执行控制器
/// </summary>
[ApiController]
[Route("api/scan")]
[Authorize]
public class ScanController : ControllerBase
{
    private readonly IScanService _scanService;

    public ScanController(IScanService scanService)
    {
        _scanService = scanService;
    }

    /// <summary>
    /// 解析二维码，返回批次信息和可用工段列表
    /// </summary>
    /// <param name="batchNo">批次号</param>
    /// <param name="processGroupId">工序组ID</param>
    [HttpGet("resolve")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ScanResolveResultDto>>> Resolve(
        [FromQuery] string batchNo,
        [FromQuery] int processGroupId)
    {
        if (string.IsNullOrWhiteSpace(batchNo))
            return BadRequest(ApiResponse<ScanResolveResultDto>.Fail("批次号不能为空"));

        var result = await _scanService.ResolveAsync(batchNo, processGroupId);
        return Ok(ApiResponse<ScanResolveResultDto>.Ok(result, "解析成功"));
    }

    /// <summary>
    /// 按批次号解析，返回批次信息和该批次下所有工序组选项
    /// </summary>
    /// <param name="batchNo">批次号</param>
    [HttpGet("batch-groups")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ScanBatchResolveResultDto>>> GetBatchProcessGroups(
        [FromQuery] string batchNo)
    {
        if (string.IsNullOrWhiteSpace(batchNo))
            return BadRequest(ApiResponse<ScanBatchResolveResultDto>.Fail("批次号不能为空"));

        var result = await _scanService.GetBatchProcessGroupsAsync(batchNo);
        return Ok(ApiResponse<ScanBatchResolveResultDto>.Ok(result, "解析成功"));
    }
}
