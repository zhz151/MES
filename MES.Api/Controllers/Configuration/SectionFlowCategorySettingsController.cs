using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.Configuration;

namespace MES.Api.Controllers.Configuration;

[ApiController]
[Route("api/section-flow-category-settings")]
[Authorize]
public class SectionFlowCategorySettingsController : ControllerBase
{
    private readonly ISectionFlowCategoryService _service;

    public SectionFlowCategorySettingsController(ISectionFlowCategoryService service)
    {
        _service = service;
    }

    /// <summary>获取所有设置</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<List<SectionFlowCategorySettingDto>>>> GetSettings()
    {
        var result = await _service.GetSettingsAsync();
        return Ok(ApiResponse<List<SectionFlowCategorySettingDto>>.Ok(result));
    }

    /// <summary>新增类别</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse>> CreateSetting([FromBody] SectionFlowCategorySettingDto dto)
    {
        var success = await _service.CreateSettingAsync(dto);
        if (!success)
            return BadRequest(ApiResponse.Fail("新增失败"));
        return Ok(ApiResponse.Ok("新增成功"));
    }

    /// <summary>删除类别（级联删组合归类行）</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse>> DeleteSetting(int id)
    {
        var success = await _service.DeleteSettingAsync(id);
        if (!success)
            return NotFound(ApiResponse.Fail("段落分类不存在"));
        return Ok(ApiResponse.Ok("删除成功"));
    }

    /// <summary>更新类别字段</summary>
    [HttpPut]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse>> SaveSetting([FromBody] SectionFlowCategorySettingDto dto)
    {
        var success = await _service.SaveSettingAsync(dto);
        if (!success)
            return NotFound(ApiResponse.Fail("段落分类不存在"));
        return Ok(ApiResponse.Ok("保存成功"));
    }
}
