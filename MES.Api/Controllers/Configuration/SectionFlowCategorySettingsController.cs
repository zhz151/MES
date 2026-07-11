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

    /// <summary>获取所有设置含明细</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<List<SectionFlowCategorySettingDto>>>> GetSettings()
    {
        var result = await _service.GetSettingsAsync();
        return Ok(ApiResponse<List<SectionFlowCategorySettingDto>>.Ok(result));
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

    /// <summary>新增明细</summary>
    [HttpPost("{settingId}/items")]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse>> CreateItem(int settingId, [FromBody] SectionFlowCategoryItemDto dto)
    {
        var success = await _service.CreateItemAsync(settingId, dto);
        if (!success)
            return NotFound(ApiResponse.Fail("段落分类不存在"));
        return Ok(ApiResponse.Ok("新增成功"));
    }

    /// <summary>更新明细系数</summary>
    [HttpPut("items/{itemId}")]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse>> SaveItem(int itemId, [FromBody] SectionFlowCategoryItemDto dto)
    {
        var success = await _service.SaveItemAsync(itemId, dto);
        if (!success)
            return NotFound(ApiResponse.Fail("明细不存在"));
        return Ok(ApiResponse.Ok("保存成功"));
    }

    /// <summary>删除明细</summary>
    [HttpDelete("items/{itemId}")]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse>> DeleteItem(int itemId)
    {
        var success = await _service.DeleteItemAsync(itemId);
        if (!success)
            return NotFound(ApiResponse.Fail("明细不存在"));
        return Ok(ApiResponse.Ok("删除成功"));
    }
}
