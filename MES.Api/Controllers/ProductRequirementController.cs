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
    /// 获取指定订单项次的产品要求
    /// </summary>
    /// <param name="orderId">订单ID（仅用于路由，实际未使用）</param>
    /// <param name="itemId">订单项次ID</param>
    [HttpGet]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProductRequirementDto>>> Get(int orderId, int itemId)
    {
        var result = await _service.GetByOrderItemIdAsync(itemId);
        if (result == null)
        {
            // 返回一个空的 DTO 对象而非 null，避免前端序列化问题
            return Ok(ApiResponse<ProductRequirementDto>.Ok(new ProductRequirementDto(), "暂无技术要求"));
        }
        return Ok(ApiResponse<ProductRequirementDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建或更新订单项次的产品要求
    /// </summary>
    /// <param name="orderId">订单ID（仅用于路由）</param>
    /// <param name="itemId">订单项次ID</param>
    /// <param name="request">产品要求请求</param>
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
    /// 删除订单项次的产品要求（软删除）
    /// </summary>
    /// <param name="orderId">订单ID（仅用于路由）</param>
    /// <param name="itemId">订单项次ID</param>
    [HttpDelete]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int orderId, int itemId)
    {
        await _service.DeleteAsync(itemId);
        return Ok(ApiResponse<object>.Ok(new object(), "删除成功"));
    }

    /// <summary>
    /// 获取订单下所有项次的产品要求列表（包含项次号）
    /// </summary>
    /// <param name="orderId">订单ID</param>
    [HttpGet("~/api/order/{orderId}/requirements")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<ProductRequirementDto>>>> GetByOrderId(int orderId)
    {
        var result = await _service.GetByOrderIdAsync(orderId);
        return Ok(ApiResponse<List<ProductRequirementDto>>.Ok(result, "查询成功"));
    }
}