using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Api.Controllers;

/// <summary>
/// 产品标准控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductionStandardController : ControllerBase
{
    private readonly IProductionStandardService _service;

    public ProductionStandardController(IProductionStandardService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取所有产品标准（用于下拉框）
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = "OrderStaff,OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<List<ProductionStandardDto>>>> GetList()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<ProductionStandardDto>>.Ok(result, "Query successful"));
    }

    /// <summary>
    /// 根据ID获取产品标准详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "OrderStaff,OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<ProductionStandardDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<ProductionStandardDto>.Ok(result, "Query successful"));
    }

    /// <summary>
    /// 创建产品标准
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<ProductionStandardDto>>> Create([FromBody] CreateProductionStandardRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<ProductionStandardDto>.Fail("Invalid request parameters"));
        }

        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<ProductionStandardDto>.Ok(result, "Create successful"));
    }

    /// <summary>
    /// 更新产品标准
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<ProductionStandardDto>>> Update(int id, [FromBody] UpdateProductionStandardRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<ProductionStandardDto>.Ok(result, "Update successful"));
    }

    /// <summary>
    /// 删除产品标准
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("Delete successful"));
    }
}