// 文件路径: MES.Api/Controllers/ProductionStandardController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

/// <summary>
/// 产品标准控制器
/// </summary>
[ApiController]
[Route("api/standard")]
[Authorize]
public class ProductionStandardController : ControllerBase
{
    private readonly IProductionStandardService _service;

    public ProductionStandardController(IProductionStandardService service)
    {
        _service = service;
    }

    /// <summary>
    /// 分页查询产品标准列表（支持关键字搜索）- 用于 ServerData 模式
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductionStandardDto>>>> GetPaged([FromQuery] QueryParams query)
    {
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<ProductionStandardDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取所有产品标准（用于下拉框）
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<ProductionStandardDto>>>> GetAll([FromQuery] bool onlyActive = true)
    {
        var result = await _service.GetAllAsync(onlyActive);
        return Ok(ApiResponse<List<ProductionStandardDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 根据ID获取产品标准详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProductionStandardDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<ProductionStandardDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建产品标准
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProductionStandardDto>>> Create([FromBody] CreateProductionStandardRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<ProductionStandardDto>.Fail("请求参数无效"));
        }

        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<ProductionStandardDto>.Ok(result, "创建成功"));
    }

    /// <summary>
    /// 更新产品标准
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProductionStandardDto>>> Update(int id, [FromBody] UpdateProductionStandardRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<ProductionStandardDto>.Fail("请求参数无效"));
        }

        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<ProductionStandardDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除产品标准
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse<object>.Ok(new object(), "删除成功"));
    }
}