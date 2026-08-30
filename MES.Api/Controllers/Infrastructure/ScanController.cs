using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Infrastructure;
using MES.Core.Interfaces.Infrastructure;
using System.Collections.Generic;

namespace MES.Api.Controllers.Infrastructure;

/// <summary>
/// 扫码执行控制器
/// </summary>
[ApiController]
[Route("api/scan")]
[Authorize]
public class ScanController : ControllerBase
{
    private readonly IScanService _scanService;
    private readonly IQrCodeService _qrCodeService;

    /// <summary>单次批量生成二维码的数量上限（打印标签场景足够，防超大请求拖垮生成）</summary>
    private const int MaxQrCodesPerRequest = 200;

    public ScanController(IScanService scanService, IQrCodeService qrCodeService)
    {
        _scanService = scanService;
        _qrCodeService = qrCodeService;
    }

    /// <summary>
    /// 解析二维码，返回批次信息和可用工段列表
    /// </summary>
    /// <param name="batchNo">批次号</param>
    /// <param name="processGroupId">工序组ID</param>
    [HttpGet("resolve")]
    [Authorize]
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
    [HttpGet("batch-groups")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<ScanBatchResolveResultDto>>> GetBatchProcessGroups(
        [FromQuery] string batchNo)
    {
        if (string.IsNullOrWhiteSpace(batchNo))
            return BadRequest(ApiResponse<ScanBatchResolveResultDto>.Fail("批次号不能为空"));

        var result = await _scanService.GetBatchProcessGroupsAsync(batchNo);
        return Ok(ApiResponse<ScanBatchResolveResultDto>.Ok(result, "解析成功"));
    }

    /// <summary>
    /// 解析设备码，返回设备信息，用于扫码报修
    /// </summary>
    [HttpGet("resolve-equipment")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<ScanEquipmentResolveResultDto>>> ResolveEquipment(
        [FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(ApiResponse<ScanEquipmentResolveResultDto>.Fail("设备编码不能为空"));

        var result = await _scanService.ResolveEquipmentAsync(code);
        return Ok(ApiResponse<ScanEquipmentResolveResultDto>.Ok(result, "解析成功"));
    }

    /// <summary>
    /// 批量生成二维码 PNG（Base64），返回顺序与输入 codes 一致，供前端打印二维码标签
    /// </summary>
    [HttpPost("qr-codes")]
    [Authorize]
    public ActionResult<ApiResponse<List<string>>> GenerateQrCodes([FromBody] QrCodesRequest request)
    {
        if (request?.Codes == null || request.Codes.Count == 0)
            return BadRequest(ApiResponse<List<string>>.Fail("二维码内容不能为空"));
        if (request.Codes.Count > MaxQrCodesPerRequest)
            return BadRequest(ApiResponse<List<string>>.Fail($"二维码数量过多（上限 {MaxQrCodesPerRequest} 个）"));

        var result = _qrCodeService.GenerateQrPngBase64(request.Codes);
        return Ok(ApiResponse<List<string>>.Ok(result, "生成成功"));
    }
}
