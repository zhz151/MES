// 文件路径: MES.Api/Controllers/GradeMappingController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

/// <summary>
/// 牌号对照控制器
/// </summary>
[ApiController]
[Route("api/grade-mapping")]
[Authorize]
public class GradeMappingController : ControllerBase
{
    private readonly IGradeMappingService _service;

    public GradeMappingController(IGradeMappingService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取所有牌号对照（用于下拉框）
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<StandardGradeMappingDto>>>> GetList()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<StandardGradeMappingDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 根据ID获取牌号对照详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<StandardGradeMappingDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<StandardGradeMappingDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建牌号对照
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<StandardGradeMappingDto>>> Create([FromBody] CreateGradeMappingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<StandardGradeMappingDto>.Fail("请求参数无效"));
        }

        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<StandardGradeMappingDto>.Ok(result, "创建成功"));
    }

    /// <summary>
    /// 更新牌号对照
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<StandardGradeMappingDto>>> Update(int id, [FromBody] UpdateGradeMappingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<StandardGradeMappingDto>.Fail("请求参数无效"));
        }

        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<StandardGradeMappingDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除牌号对照
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        // 修复 CS8625：使用 new object() 代替 null
        return Ok(ApiResponse<object>.Ok(new object(), "删除成功"));
    }
}