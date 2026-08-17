using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.Configuration;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Configuration;

/// <summary>
/// 质量证明书打印列布局配置控制器：「字段布局」面板全量加载/批量保存（全局共享）。
/// </summary>
[ApiController]
[Route(ApiEndpoints.CertificatePrintColumnDefinition)]
[Authorize]
public class CertificatePrintColumnDefinitionController : ControllerBase
{
    private readonly ICertificatePrintColumnDefinitionService _service;

    public CertificatePrintColumnDefinitionController(ICertificatePrintColumnDefinitionService service)
    {
        _service = service;
    }

    /// <summary>全量配置（「字段布局」面板加载），按 BlockKey 升序 + ColumnIndex 升序</summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.QualityRead)]
    public async Task<ActionResult<ApiResponse<List<CertificatePrintColumnDefinitionDto>>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<CertificatePrintColumnDefinitionDto>>.Ok(result));
    }

    /// <summary>配置映射：BlockKey|FieldKey → 配置 DTO（打印链路覆盖默认列定义用）</summary>
    [HttpGet("config-map")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, CertificatePrintColumnDefinitionDto>>>> GetConfigMap()
    {
        var result = await _service.GetConfigMapAsync();
        return Ok(ApiResponse<Dictionary<string, CertificatePrintColumnDefinitionDto>>.Ok(result));
    }

    /// <summary>批量新增/更新（锚点 BlockKey+FieldKey），返回写入行数</summary>
    [HttpPost("save-all")]
    [Authorize(Roles = Roles.Policies.QualityWrite)]
    public async Task<ActionResult<ApiResponse<int>>> SaveAll([FromBody] List<CertificatePrintColumnDefinitionDto> items)
    {
        var result = await _service.SaveAllAsync(items);
        return Ok(ApiResponse<int>.Ok(result));
    }
}
