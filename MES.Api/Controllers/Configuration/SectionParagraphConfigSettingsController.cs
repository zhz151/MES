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

    /// <summary>获取所有设置（段落由 3 类配置自动生成，仅参数可编辑）</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Policies.ConfigurationView)]
    public async Task<ActionResult<ApiResponse<List<SectionParagraphConfigDto>>>> GetSettings()
    {
        var result = await _service.GetSettingsAsync();
        return Ok(ApiResponse<List<SectionParagraphConfigDto>>.Ok(result));
    }

    /// <summary>更新段落参数（日流转设定/偏少天数/过多天数/备注）</summary>
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
