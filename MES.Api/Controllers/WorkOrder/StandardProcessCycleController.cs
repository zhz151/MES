// 文件路径: MES.Api/Controllers/StandardProcessCycleController.cs
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

/// <summary>
/// 标准工艺生产周期控制器
/// </summary>
[ApiController]
[Route("api/standard-process-cycle")]
[Authorize]
public class StandardProcessCycleController : ControllerBase
{
    private readonly IStandardProcessCycleService _service;

    public StandardProcessCycleController(IStandardProcessCycleService service)
    {
        _service = service;
    }

    /// <summary>
    /// 分页查询标准工艺生产周期列表
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<StandardProcessCycleDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        QueryParams query = new() { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword, SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy, IsDescending = isDescending };
        if (!string.IsNullOrEmpty(filters))
        {
            try
            {
                var f = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (f != null && f.Count > 0) query.Filters = f;
            }
            catch { }
        }
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<StandardProcessCycleDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取所有标准工艺生产周期
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<StandardProcessCycleDto>>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<StandardProcessCycleDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 根据ID获取详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<StandardProcessCycleDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<StandardProcessCycleDto>.Fail("标准工艺生产周期不存在"));
        return Ok(ApiResponse<StandardProcessCycleDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建标准工艺生产周期
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<StandardProcessCycleDto>>> Create([FromBody] CreateStandardProcessCycleRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<StandardProcessCycleDto>.Fail("请求参数无效"));

        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<StandardProcessCycleDto>.Ok(result, "创建成功"));
    }

    /// <summary>
    /// 更新标准工艺生产周期
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<StandardProcessCycleDto>>> Update(int id, [FromBody] UpdateStandardProcessCycleRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<StandardProcessCycleDto>.Fail("请求参数无效"));

        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<StandardProcessCycleDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除标准工艺生产周期
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse<object>.Ok(new object(), "删除成功"));
    }

    /// <summary>
    /// 获取筛选上下文（各列去重值）
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }
}
