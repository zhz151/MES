using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Warehouse;
using MES.Core.Interfaces.Warehouse;

namespace MES.Api.Controllers.Warehouse;

[ApiController]
[Route("api/warehouse")]
[Authorize]
public class WarehouseController : ControllerBase
{
    private readonly IWarehouseService _service;

    public WarehouseController(IWarehouseService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.WarehouseView)]
    public async Task<ActionResult<ApiResponse<PagedResult<WarehouseDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        QueryParams query = new() { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword, SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy, IsDescending = isDescending };
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetPagedAsync(query, isActive);
        return Ok(ApiResponse<PagedResult<WarehouseDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.WarehouseView)]
    public async Task<ActionResult<ApiResponse<List<WarehouseDto>>>> GetAll([FromQuery] bool onlyActive = true)
    {
        var result = await _service.GetAllAsync(onlyActive);
        return Ok(ApiResponse<List<WarehouseDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.WarehouseView)]
    public async Task<ActionResult<ApiResponse<WarehouseDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<WarehouseDto>.Ok(result, "查询成功"));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Policies.WarehouseEdit)]
    public async Task<ActionResult<ApiResponse<WarehouseDto>>> Create([FromBody] CreateWarehouseRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<WarehouseDto>.Fail("请求参数无效"));

        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<WarehouseDto>.Ok(result, "创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.WarehouseEdit)]
    public async Task<ActionResult<ApiResponse<WarehouseDto>>> Update(int id, [FromBody] UpdateWarehouseRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<WarehouseDto>.Fail("请求参数无效"));

        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<WarehouseDto>.Ok(result, "更新成功"));
    }

    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.WarehouseView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.WarehouseDelete)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse<bool>.Ok(true, "删除成功"));
    }
}
