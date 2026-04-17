// 鏂囦欢璺緞: MES.Api/Controllers/GradeMappingController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Api.Controllers;

/// <summary>
/// 鐗屽彿瀵圭収鎺у埗鍣?/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GradeMappingController : ControllerBase
{
    private readonly IGradeMappingService _service;

    public GradeMappingController(IGradeMappingService service)
    {
        _service = service;
    }

    /// <summary>
    /// 鑾峰彇鎵€鏈夌墝鍙峰鐓э紙鐢ㄤ簬涓嬫媺妗嗭級
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = "OrderStaff,OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<List<StandardGradeMappingDto>>>> GetList()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<StandardGradeMappingDto>>.Ok(result, "鏌ヨ鎴愬姛"));
    }

    /// <summary>
    /// 鏍规嵁ID鑾峰彇鐗屽彿瀵圭収璇︽儏
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "OrderStaff,OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<StandardGradeMappingDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<StandardGradeMappingDto>.Ok(result, "鏌ヨ鎴愬姛"));
    }

    /// <summary>
    /// 鍒涘缓鐗屽彿瀵圭収
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<StandardGradeMappingDto>>> Create([FromBody] CreateGradeMappingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<StandardGradeMappingDto>.Fail("璇锋眰鍙傛暟鏃犳晥"));
        }

        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<StandardGradeMappingDto>.Ok(result, "鍒涘缓鎴愬姛"));
    }

    /// <summary>
    /// 鏇存柊鐗屽彿瀵圭収
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<StandardGradeMappingDto>>> Update(int id, [FromBody] UpdateGradeMappingRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<StandardGradeMappingDto>.Ok(result, "鏇存柊鎴愬姛"));
    }

    /// <summary>
    /// 鍒犻櫎鐗屽彿瀵圭収
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("鍒犻櫎鎴愬姛"));
    }
}