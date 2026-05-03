using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

[ApiController]
[Route("api/supplier")]
[Authorize]
public class SupplierController : ControllerBase
{
    private readonly ISupplierService _service;
    private readonly ILogger<SupplierController> _logger;

    public SupplierController(ISupplierService service, ILogger<SupplierController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<SupplierProfileDto>>>> GetPaged(
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
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy,
            IsDescending = isDescending
        };
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<SupplierProfileDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SupplierProfileDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<SupplierProfileDto>.Ok(result, "查询成功"));
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SupplierProfileDto>>> Create([FromBody] CreateSupplierRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SupplierProfileDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<SupplierProfileDto>.Ok(result, "创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SupplierProfileDto>>> Update(int id, [FromBody] UpdateSupplierRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SupplierProfileDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<SupplierProfileDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpGet("active")]
    [Authorize(Roles = $"{Roles.Staffs.Material},{Roles.Directors.Material},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<SupplierProfileDto>>>> GetActive()
    {
        var result = await _service.GetActiveAsync();
        return Ok(ApiResponse<List<SupplierProfileDto>>.Ok(result, "查询成功"));
    }
}
