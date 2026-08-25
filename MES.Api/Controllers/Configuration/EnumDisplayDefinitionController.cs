using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.Configuration;
using MES.Shared.Constants;
using System.Text.Json;

namespace MES.Api.Controllers.Configuration;

[ApiController]
[Route("api/enum-display-definition")]
[Authorize]
public class EnumDisplayDefinitionController : ControllerBase
{
    private readonly IEnumDisplayDefinitionService _service;

    public EnumDisplayDefinitionController(IEnumDisplayDefinitionService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.ConfigurationView)]
    public async Task<ActionResult<ApiResponse<PagedResult<EnumDisplayDefinitionDto>>>> GetPaged(
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
        return Ok(ApiResponse<PagedResult<EnumDisplayDefinitionDto>>.Ok(result));
    }

    /// <summary>全量显示映射：EnumKey → Value → DisplayName（配置表优先，兜底 EnumHelper），前端显示层用</summary>
    [HttpGet("display-map")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, Dictionary<string, string>>>>> GetDisplayMap()
    {
        var result = await _service.GetDisplayMapAsync();
        return Ok(ApiResponse<Dictionary<string, Dictionary<string, string>>>.Ok(result));
    }

    /// <summary>全量显示选项：EnumKey → 有序 (Value/DisplayName/DisplayOrder)，供前端排序注入</summary>
    [HttpGet("options-map")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<EnumDisplayOptionDto>>>>> GetOptionsMap()
    {
        var result = await _service.GetOptionsMapAsync();
        return Ok(ApiResponse<Dictionary<string, List<EnumDisplayOptionDto>>>.Ok(result));
    }

    /// <summary>列筛选上下文：可筛列的 DISTINCT 值，供前端 ExcelFilter 下拉加载</summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.ConfigurationView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    /// <summary>恢复默认：为该 EnumKey 生成静态兜底（EnumHelper）中缺失的默认行，返回新增行数</summary>
    [HttpPost("restore-defaults")]
    [Authorize(Roles = Roles.Policies.ConfigurationEdit)]
    public async Task<ActionResult<ApiResponse<int>>> RestoreDefaults([FromQuery] string? key)
    {
        var result = await _service.RestoreDefaultsAsync(key ?? string.Empty);
        return Ok(ApiResponse<int>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.ConfigurationView)]
    public async Task<ActionResult<ApiResponse<EnumDisplayDefinitionDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<EnumDisplayDefinitionDto>.Fail("枚举显示配置不存在"));
        return Ok(ApiResponse<EnumDisplayDefinitionDto>.Ok(result));
    }

    [HttpPost("save")]
    [Authorize(Roles = Roles.Policies.ConfigurationEdit)]
    public async Task<ActionResult<ApiResponse<bool>>> Save([FromBody] EnumDisplayDefinitionDto dto)
    {
        var result = await _service.SaveAsync(dto);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpPost("delete/{id}")]
    [Authorize(Roles = Roles.Policies.ConfigurationEdit)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        return Ok(ApiResponse<bool>.Ok(result));
    }
}
