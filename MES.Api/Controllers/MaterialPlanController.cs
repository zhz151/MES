using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

[ApiController]
[Route("api/material-plan")]
[Authorize]
public class MaterialPlanController : ControllerBase
{
    private readonly IMaterialPlanService _materialPlanService;

    public MaterialPlanController(IMaterialPlanService materialPlanService)
    {
        _materialPlanService = materialPlanService;
    }

    #region 原料采购计划

    [HttpGet("semi/{workOrderId}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<PurchaseSemiPlanDto>>>> GetSemiPlans(int workOrderId)
    {
        var result = await _materialPlanService.GetSemiPlansAsync(workOrderId);
        return Ok(ApiResponse<List<PurchaseSemiPlanDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("semi/detail/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PurchaseSemiPlanDto>>> GetSemiPlanById(int id)
    {
        var result = await _materialPlanService.GetSemiPlanByIdAsync(id);
        return Ok(ApiResponse<PurchaseSemiPlanDto>.Ok(result, "查询成功"));
    }

    [HttpPost("semi")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PurchaseSemiPlanDto>>> CreateSemiPlan(
        [FromBody] CreatePurchaseSemiPlanRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PurchaseSemiPlanDto>.Fail("请求参数无效"));

        var result = await _materialPlanService.CreateSemiPlanAsync(request);
        return Ok(ApiResponse<PurchaseSemiPlanDto>.Ok(result, "创建成功"));
    }

    [HttpDelete("semi/{id}")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> DeleteSemiPlan(int id)
    {
        await _materialPlanService.DeleteSemiPlanAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    #endregion

    #region 成品采购计划

    [HttpGet("finished/{workOrderId}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<PurchaseFinishedPlanDto>>>> GetFinishedPlans(int workOrderId)
    {
        var result = await _materialPlanService.GetFinishedPlansAsync(workOrderId);
        return Ok(ApiResponse<List<PurchaseFinishedPlanDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("finished/detail/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PurchaseFinishedPlanDto>>> GetFinishedPlanById(int id)
    {
        var result = await _materialPlanService.GetFinishedPlanByIdAsync(id);
        return Ok(ApiResponse<PurchaseFinishedPlanDto>.Ok(result, "查询成功"));
    }

    [HttpPost("finished")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PurchaseFinishedPlanDto>>> CreateFinishedPlan(
        [FromBody] CreatePurchaseFinishedPlanRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PurchaseFinishedPlanDto>.Fail("请求参数无效"));

        var result = await _materialPlanService.CreateFinishedPlanAsync(request);
        return Ok(ApiResponse<PurchaseFinishedPlanDto>.Ok(result, "创建成功"));
    }

    [HttpDelete("finished/{id}")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> DeleteFinishedPlan(int id)
    {
        await _materialPlanService.DeleteFinishedPlanAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    #endregion

    #region 用料测算

    [HttpPost("calculate")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<MaterialCalculateResult>>> Calculate(
        [FromBody] MaterialCalculateRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<MaterialCalculateResult>.Fail("请求参数无效"));

        var result = await _materialPlanService.CalculateAsync(request);
        return Ok(ApiResponse<MaterialCalculateResult>.Ok(result, "测算完成"));
    }

    #endregion

    #region 计划状态

    [HttpGet("summary/{workOrderId}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<WorkOrderMaterialPlanDto>>> GetSummary(int workOrderId)
    {
        var result = await _materialPlanService.GetWorkOrderMaterialPlanAsync(workOrderId);
        return Ok(ApiResponse<WorkOrderMaterialPlanDto>.Ok(result, "查询成功"));
    }

    [HttpPost("refresh-status/{workOrderId}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> RefreshStatus(int workOrderId)
    {
        await _materialPlanService.UpdateMaterialPlanStatusAsync(workOrderId);
        return Ok(ApiResponse.Ok("状态已刷新"));
    }

    #endregion

    #region 打印

    [HttpGet("print/semi/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintSemiPlan(int id)
    {
        var bytes = await _materialPlanService.PrintSemiPlanAsync(id);
        var base64 = Convert.ToBase64String(bytes);
        return Ok(ApiResponse<string>.Ok(base64, "生成成功"));
    }

    [HttpGet("print/finished/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintFinishedPlan(int id)
    {
        var bytes = await _materialPlanService.PrintFinishedPlanAsync(id);
        var base64 = Convert.ToBase64String(bytes);
        return Ok(ApiResponse<string>.Ok(base64, "生成成功"));
    }

    #endregion
}
