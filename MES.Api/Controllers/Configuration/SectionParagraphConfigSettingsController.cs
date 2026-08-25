using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.Configuration;

namespace MES.Api.Controllers.Configuration;

[ApiController]
[Route("api/section-paragraph-config-settings")]
[Authorize]
public class SectionParagraphConfigSettingsController : ControllerBase
{
    private readonly ISectionParagraphConfigService _service;

    public SectionParagraphConfigSettingsController(ISectionParagraphConfigService service)
    {
        _service = service;
    }

    /// <summary>获取所有设置</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Policies.ConfigurationView)]
    public async Task<ActionResult<ApiResponse<List<SectionParagraphConfigDto>>>> GetSettings()
    {
        var result = await _service.GetSettingsAsync();
        return Ok(ApiResponse<List<SectionParagraphConfigDto>>.Ok(result));
    }

    /// <summary>新增段落</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Policies.ConfigurationEdit)]
    public async Task<ActionResult<ApiResponse>> CreateSetting([FromBody] SectionParagraphConfigDto dto)
    {
        var success = await _service.CreateSettingAsync(dto);
        if (!success)
            return BadRequest(ApiResponse.Fail("新增失败"));
        return Ok(ApiResponse.Ok("新增成功"));
    }

    /// <summary>删除段落（组合归类表「归属段落」置空）</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Policies.ConfigurationDelete)]
    public async Task<ActionResult<ApiResponse>> DeleteSetting(int id)
    {
        var success = await _service.DeleteSettingAsync(id);
        if (!success)
            return NotFound(ApiResponse.Fail("段落分类不存在"));
        return Ok(ApiResponse.Ok("删除成功"));
    }

    /// <summary>更新段落字段</summary>
    [HttpPut]
    [Authorize(Roles = Roles.Policies.ConfigurationEdit)]
    public async Task<ActionResult<ApiResponse>> SaveSetting([FromBody] SectionParagraphConfigDto dto)
    {
        var success = await _service.SaveSettingAsync(dto);
        if (!success)
            return NotFound(ApiResponse.Fail("段落分类不存在"));
        return Ok(ApiResponse.Ok("保存成功"));
    }
}
