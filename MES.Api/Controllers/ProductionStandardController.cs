// 鏂囦欢璺緞: MES.Api/Controllers/ProductionStandardController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Api.Controllers;

/// <summary>
/// 浜у搧鏍囧噯鎺у埗鍣?/// </summary>
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
    /// 鑾峰彇鎵€鏈変骇鍝佹爣鍑嗭紙鐢ㄤ簬涓嬫媺妗嗭級
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = "OrderStaff,OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<List<ProductionStandardDto>>>> GetList()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<ProductionStandardDto>>.Ok(result, "鏌ヨ鎴愬姛"));
    }

    /// <summary>
    /// 鏍规嵁ID鑾峰彇浜у搧鏍囧噯璇︽儏
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "OrderStaff,OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<ProductionStandardDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<ProductionStandardDto>.Ok(result, "鏌ヨ鎴愬姛"));
    }

    /// <summary>
    /// 鍒涘缓浜у搧鏍囧噯
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<ProductionStandardDto>>> Create([FromBody] CreateProductionStandardRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<ProductionStandardDto>.Fail("璇锋眰鍙傛暟鏃犳晥"));
        }

        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<ProductionStandardDto>.Ok(result, "鍒涘缓鎴愬姛"));
    }

    /// <summary>
    /// 鏇存柊浜у搧鏍囧噯
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<ProductionStandardDto>>> Update(int id, [FromBody] UpdateProductionStandardRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<ProductionStandardDto>.Ok(result, "鏇存柊鎴愬姛"));
    }

    /// <summary>
    /// 鍒犻櫎浜у搧鏍囧噯
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("鍒犻櫎鎴愬姛"));
    }
}