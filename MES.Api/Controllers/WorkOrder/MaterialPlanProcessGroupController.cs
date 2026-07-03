using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.WorkOrder;

/// <summary>
/// 用料计划工序组控制器
/// planType: 1=PurchaseSemiPlan, 3=InventoryPlan, 4=RoundBarPiercingPlan
/// </summary>
[ApiController]
[Route("api/material-plan/{planType}/process-groups")]
[Authorize]
public class MaterialPlanProcessGroupController : ControllerBase
{
    private readonly IMaterialPlanProcessGroupService _service;

    public MaterialPlanProcessGroupController(IMaterialPlanProcessGroupService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取指定用料计划的工序组列表
    /// </summary>
    [HttpGet("{planId}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<MaterialPlanProcessGroupDto>>>> GetByPlan(
        int planType, int planId)
    {
        var result = await _service.GetByPlanAsync(planType, planId);
        return Ok(ApiResponse<List<MaterialPlanProcessGroupDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 保存用料计划工序组（全量替换）
    /// </summary>
    [HttpPost("{planId}/save")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<bool>>> Save(
        int planType, int planId, [FromBody] List<SavePlanProcessGroupItem> items)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("请求参数无效"));

        await _service.SaveAsync(planType, planId, items);
        return Ok(ApiResponse<bool>.Ok(true, "保存成功"));
    }
}
