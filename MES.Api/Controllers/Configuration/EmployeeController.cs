using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.Configuration;
using System.Text.Json;

namespace MES.Api.Controllers.Configuration;

[ApiController]
[Route("api/employee")]
[Authorize]
public class EmployeeController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeeController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    /// <summary>
    /// 分页查询
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<PagedResult<EmployeeDto>>>> GetPaged(
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
        var result = await _employeeService.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<EmployeeDto>>.Ok(result));
    }

    /// <summary>
    /// 按工号查询（扫码用）
    /// </summary>
    [HttpGet("{code}")]
    [Authorize(Roles = $"{Roles.Staffs.Batch},{Roles.Directors.Batch},{Roles.Staffs.Quality},{Roles.Directors.Quality},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> GetByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(ApiResponse<EmployeeDto>.Fail("工号不能为空"));

        var result = await _employeeService.GetByCodeAsync(code);
        if (result == null)
            return NotFound(ApiResponse<EmployeeDto>.Fail("未找到员工"));

        return Ok(ApiResponse<EmployeeDto>.Ok(result));
    }

    /// <summary>
    /// 新增或更新
    /// </summary>
    [HttpPost("save")]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<bool>>> Save([FromBody] EmployeeDto dto)
    {
        var result = await _employeeService.SaveAsync(dto);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    /// <summary>
    /// 删除
    /// </summary>
    [HttpPost("delete/{id}")]
    [Authorize(Roles = Roles.Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _employeeService.DeleteAsync(id);
        return Ok(ApiResponse<bool>.Ok(result));
    }
}
