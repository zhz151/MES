using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.ProductionStandard;
using MES.Core.Interfaces.ProductionStandard;

namespace MES.Api.Controllers.ProductionStandard;

[ApiController]
[Route(ApiEndpoints.StandardRegister)]
[Authorize]
public class StandardRegisterController : ControllerBase
{
    private readonly IStandardRegisterService _service;

    public StandardRegisterController(IStandardRegisterService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<PagedResult<StandardRegisterDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "StandardNo" : sortBy,
            IsDescending = isDescending
        };
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<StandardRegisterDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<StandardRegisterDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<StandardRegisterDto>.Fail("标准号不存在"));
        return Ok(ApiResponse<StandardRegisterDto>.Ok(result));
    }

    [HttpPost("save")]
    public async Task<ActionResult<ApiResponse<int>>> Save([FromBody] StandardRegisterDto dto)
    {
        var result = await _service.SaveAsync(dto);
        return Ok(ApiResponse<int>.Ok(result));
    }

    [HttpPost("delete/{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpGet("all")]
    public async Task<ActionResult<ApiResponse<List<StandardRegisterDto>>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<StandardRegisterDto>>.Ok(result));
    }

    /// <summary>
    /// 根据标准号解析标准名称，用于前端自动填充
    /// </summary>
    [HttpGet("resolve-name")]
    public async Task<ActionResult<ApiResponse<string?>>> ResolveName([FromQuery] string? standardNo)
    {
        if (string.IsNullOrWhiteSpace(standardNo))
            return Ok(ApiResponse<string?>.Ok(null));
        var result = await _service.ResolveNameAsync(standardNo);
        return Ok(ApiResponse<string?>.Ok(result));
    }

    [HttpGet("filter-contexts")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    // ========== 子项目 ==========

    [HttpGet("{id}/items")]
    public async Task<ActionResult<ApiResponse<List<StandardRegisterItemDto>>>> GetItems(int id)
    {
        var result = await _service.GetItemsAsync(id);
        return Ok(ApiResponse<List<StandardRegisterItemDto>>.Ok(result));
    }

    [HttpPost("item/save")]
    public async Task<ActionResult<ApiResponse<int>>> SaveItem([FromBody] StandardRegisterItemDto dto)
    {
        var result = await _service.SaveItemAsync(dto);
        return Ok(ApiResponse<int>.Ok(result));
    }

    [HttpPost("item/delete/{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteItem(int id)
    {
        var result = await _service.DeleteItemAsync(id);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpPost("cleanup-orphaned-items")]
    public async Task<ActionResult<ApiResponse<int>>> CleanupOrphanedItems()
    {
        var count = await _service.CleanupOrphanedItemsAsync();
        return Ok(ApiResponse<int>.Ok(count));
    }
}
