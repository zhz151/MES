using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.Configuration;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Configuration;

/// <summary>
/// 质量证明书打印配置控制器：列表页「打印设置」对话框全量加载/批量保存（全局共享）。
/// </summary>
[ApiController]
[Route(ApiEndpoints.CertificatePrintSetting)]
[Authorize]
public class CertificatePrintSettingController : ControllerBase
{
    private readonly ICertificatePrintSettingService _service;

    public CertificatePrintSettingController(ICertificatePrintSettingService service)
    {
        _service = service;
    }

    /// <summary>全量配置（「打印设置」对话框加载），按 Key 升序</summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.QualityRead)]
    public async Task<ActionResult<ApiResponse<List<CertificatePrintSettingDto>>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<CertificatePrintSettingDto>>.Ok(result));
    }

    /// <summary>批量新增/更新（锚点 Key），返回写入行数</summary>
    [HttpPost("save-all")]
    [Authorize(Roles = Roles.Policies.QualityWrite)]
    public async Task<ActionResult<ApiResponse<int>>> SaveAll([FromBody] List<CertificatePrintSettingDto> items)
    {
        var result = await _service.SaveAllAsync(items);
        return Ok(ApiResponse<int>.Ok(result));
    }
}
