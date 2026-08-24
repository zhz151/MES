using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.Configuration;
using MES.Shared.Constants;
using System.Text.Json;

namespace MES.Api.Controllers.Configuration;

[ApiController]
[Route("api/dict-value-definition")]
[Authorize]
public class DictValueDefinitionController : ControllerBase
{
    private readonly IDictValueDefinitionService _service;

    public DictValueDefinitionController(IDictValueDefinitionService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.ConfigurationRead)]
    public async Task<ActionResult<ApiResponse<PagedResult<DictValueDefinitionDto>>>> GetPaged(
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
            query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<DictValueDefinitionDto>>.Ok(result));
    }

    /// <summary>全量显示映射：DictKey → Value → DisplayName（配置表优先，兜底 DictValueDefaults），前端显示层用</summary>
    [HttpGet("display-map")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, Dictionary<string, string>>>>> GetDisplayMap()
    {
        var result = await _service.GetDisplayMapAsync();
        return Ok(ApiResponse<Dictionary<string, Dictionary<string, string>>>.Ok(result));
    }

    /// <summary>启用字典值列表：配置表 IsEnabled=true 按 DisplayOrder 升序 + 静态兜底追加末尾（下拉选项动态加载用）</summary>
    [HttpGet("enabled-values")]
    public async Task<ActionResult<ApiResponse<List<DictValueInfoDto>>>> GetEnabledValues([FromQuery] string? key)
    {
        var result = await _service.GetEnabledValuesAsync(key ?? string.Empty);
        return Ok(ApiResponse<List<DictValueInfoDto>>.Ok(result));
    }

    /// <summary>列筛选上下文：可筛列的 DISTINCT 值，供前端 ExcelFilter 下拉加载</summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.ConfigurationRead)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    /// <summary>恢复默认：为该 DictKey 生成静态兜底（DictValueDefaults）中缺失的默认行，返回新增行数</summary>
    [HttpPost("restore-defaults")]
    [Authorize(Roles = Roles.Policies.ConfigurationWrite)]
    public async Task<ActionResult<ApiResponse<int>>> RestoreDefaults([FromQuery] string? key)
    {
        var result = await _service.RestoreDefaultsAsync(key ?? string.Empty);
        return Ok(ApiResponse<int>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.ConfigurationRead)]
    public async Task<ActionResult<ApiResponse<DictValueDefinitionDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<DictValueDefinitionDto>.Fail("字典值配置不存在"));
        return Ok(ApiResponse<DictValueDefinitionDto>.Ok(result));
    }

    [HttpPost("save")]
    [Authorize(Roles = Roles.Policies.ConfigurationWrite)]
    public async Task<ActionResult<ApiResponse<bool>>> Save([FromBody] DictValueDefinitionDto dto)
    {
        var result = await _service.SaveAsync(dto);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpPost("delete/{id}")]
    [Authorize(Roles = Roles.Policies.ConfigurationWrite)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        return Ok(ApiResponse<bool>.Ok(result));
    }
}
