// 文件路径: MES.Api/Controllers/ProductRequirementController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces.Order;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

/// <summary>
/// 产品要求控制器
/// </summary>
[ApiController]
[Route("api/order/{orderId}/items/{itemId}/requirement")]
[Authorize]
public class ProductRequirementController : ControllerBase
{
    private readonly IProductRequirementService _service;

    public ProductRequirementController(IProductRequirementService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取订单项次的产品要求
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProductRequirementDto>>> Get(int orderId, int itemId)
    {
        var result = await _service.GetByOrderItemIdAsync(itemId);
        if (result == null)
        {
            // 修复：使用 default! 或创建空对象，避免 null 引用警告
            return Ok(ApiResponse<ProductRequirementDto>.Ok(default!, "暂无技术要求"));
        }
        return Ok(ApiResponse<ProductRequirementDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建或更新产品要求
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProductRequirementDto>>> CreateOrUpdate(
        int orderId, 
        int itemId, 
        [FromBody] CreateProductRequirementRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<ProductRequirementDto>.Fail("请求参数无效"));
        }

        var result = await _service.CreateOrUpdateAsync(itemId, request);
        return Ok(ApiResponse<ProductRequirementDto>.Ok(result, "保存成功"));
    }

    /// <summary>
    /// 删除产品要求
    /// </summary>
    [HttpDelete]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int orderId, int itemId)
    {
        await _service.DeleteAsync(itemId);
        // 修复：使用空对象而不是 null
        return Ok(ApiResponse<object>.Ok(new object(), "删除成功"));
    }
}