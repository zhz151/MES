using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

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
    [Authorize(Roles = "BatchStaff,BatchDirector,Admin")]
    public async Task<ActionResult<ApiResponse<ScanResolveResultDto>>> Resolve(
        [FromQuery] string batchNo,
        [FromQuery] int processGroupId)
    {
        if (string.IsNullOrWhiteSpace(batchNo))
            return BadRequest(ApiResponse<ScanResolveResultDto>.Fail("批次号不能为空"));

        var result = await _scanService.ResolveAsync(batchNo, processGroupId);
        return Ok(ApiResponse<ScanResolveResultDto>.Ok(result, "解析成功"));
    }
}
