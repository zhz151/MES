// 文件路径: MES.Api/Controllers/ProductRequirementController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Order;
using MES.Core.Interfaces.Order;

namespace MES.Api.Controllers.Order;

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
    /// <param name="itemId">订单项次ID</param>
    [HttpGet]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProductRequirementDto>>> Get(int itemId)
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
    /// <param name="itemId">订单项次ID</param>
    /// <param name="request">产品要求请求</param>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProductRequirementDto>>> CreateOrUpdate(
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

    /// <summary>
    /// 按标准号获取新建技术要求的默认值（工厂检验项要求含"必检"→true）
    /// </summary>
    /// <param name="standardNo">标准号</param>
    [HttpGet("~/api/order/{orderId}/requirements/defaults")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<ProductRequirementDefaultsDto>>> GetDefaults(string? standardNo)
    {
        var result = await _service.GetDefaultRequirementsByStandardNoAsync(standardNo);
        return Ok(ApiResponse<ProductRequirementDefaultsDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 按工厂检验项要求全面回填所有技术要求（按订单项次标准号匹配，含"必检"→true；液压检验仅定尺）
    /// </summary>
    [HttpPost("~/api/order/requirements/refresh-all-defaults")]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<int>>> RefreshDefaultsAll()
    {
        var count = await _service.RefreshDefaultsAllAsync();
        return Ok(ApiResponse<int>.Ok(count, $"已回填 {count} 条技术要求"));
    }

    /// <summary>
    /// 按工单关联订单项次ID列表（逗号分隔）取质量备注（各项次技术要求「其他要求」按项次号拼接）
    /// </summary>
    /// <param name="orderItemIds">逗号分隔的订单项次ID列表</param>
    [HttpGet("~/api/order/requirements/quality-remark")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> GetQualityRemark(string? orderItemIds)
    {
        var result = await _service.GetQualityRemarkByOrderItemIdsAsync(orderItemIds);
        return Ok(ApiResponse<string>.Ok(result, "查询成功"));
    }
}