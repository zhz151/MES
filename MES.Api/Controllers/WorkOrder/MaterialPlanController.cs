using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Interfaces.WorkOrder;

namespace MES.Api.Controllers.WorkOrder;

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

    [HttpPut("semi/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PurchaseSemiPlanDto>>> UpdateSemiPlan(
        int id, [FromBody] CreatePurchaseSemiPlanRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PurchaseSemiPlanDto>.Fail("请求参数无效"));

        var result = await _materialPlanService.UpdateSemiPlanAsync(id, request);
        return Ok(ApiResponse<PurchaseSemiPlanDto>.Ok(result, "保存成功"));
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

    [HttpPost("finished/batch")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<PurchaseFinishedPlanDto>>>> CreateFinishedPlanBatch(
        [FromBody] List<CreatePurchaseFinishedPlanRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<PurchaseFinishedPlanDto>>.Fail("请求参数无效"));
        if (requests.Count == 0)
            return BadRequest(ApiResponse<List<PurchaseFinishedPlanDto>>.Fail("请求列表不能为空"));

        var result = await _materialPlanService.CreateFinishedPlanBatchAsync(requests);
        return Ok(ApiResponse<List<PurchaseFinishedPlanDto>>.Ok(result, "批量创建成功"));
    }

    [HttpPut("finished/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PurchaseFinishedPlanDto>>> UpdateFinishedPlan(
        int id, [FromBody] CreatePurchaseFinishedPlanRequest request)
    {
        var result = await _materialPlanService.UpdateFinishedPlanAsync(id, request);
        return Ok(ApiResponse<PurchaseFinishedPlanDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("finished/{id}")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> DeleteFinishedPlan(int id)
    {
        await _materialPlanService.DeleteFinishedPlanAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    #endregion

    #region 库存使用计划

    [HttpGet("inventory/{workOrderId}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<InventoryPlanDto>>>> GetInventoryPlans(int workOrderId)
    {
        var result = await _materialPlanService.GetInventoryPlansAsync(workOrderId);
        return Ok(ApiResponse<List<InventoryPlanDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("rework/{workOrderId}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<InventoryPlanDto>>>> GetReworkPlans(int workOrderId)
    {
        var result = await _materialPlanService.GetReworkPlansAsync(workOrderId);
        return Ok(ApiResponse<List<InventoryPlanDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("inventory/available/{workOrderId}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<AvailableInventoryBatchDto>>>> GetAvailableInventory(
        int workOrderId, [FromQuery] int? excludePlanId = null)
    {
        var result = await _materialPlanService.GetAvailableInventoryAsync(workOrderId, excludePlanId);
        return Ok(ApiResponse<List<AvailableInventoryBatchDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("rework-inventory/{workOrderId}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<AvailableInventoryBatchDto>>>> GetAvailableReworkInventory(
        int workOrderId, [FromQuery] ReworkType reworkType, [FromQuery] int? excludePlanId = null)
    {
        var result = await _materialPlanService.GetAvailableReworkInventoryAsync(workOrderId, reworkType, excludePlanId);
        return Ok(ApiResponse<List<AvailableInventoryBatchDto>>.Ok(result, "查询成功"));
    }

    [HttpPost("inventory")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<InventoryPlanDto>>> CreateInventoryPlan(
        [FromBody] CreateInventoryPlanRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<InventoryPlanDto>.Fail("请求参数无效"));

        var result = await _materialPlanService.CreateInventoryPlanAsync(request);
        return Ok(ApiResponse<InventoryPlanDto>.Ok(result, "创建成功"));
    }

    [HttpPost("inventory/batch")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<InventoryPlanDto>>>> CreateInventoryPlanBatch(
        [FromBody] List<CreateInventoryPlanRequest> requests)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<List<InventoryPlanDto>>.Fail("请求参数无效"));
        if (requests.Count == 0)
            return BadRequest(ApiResponse<List<InventoryPlanDto>>.Fail("请求列表不能为空"));

        var result = await _materialPlanService.CreateInventoryPlanBatchAsync(requests);
        return Ok(ApiResponse<List<InventoryPlanDto>>.Ok(result, "批量创建成功"));
    }

    [HttpGet("inventory/plan/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<InventoryPlanDto>>> GetInventoryPlanById(int id)
    {
        var result = await _materialPlanService.GetInventoryPlanByIdAsync(id);
        return Ok(ApiResponse<InventoryPlanDto>.Ok(result, "查询成功"));
    }

    [HttpPut("inventory/{id}")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<InventoryPlanDto>>> UpdateInventoryPlan(
        int id, [FromBody] CreateInventoryPlanRequest request)
    {
        var result = await _materialPlanService.UpdateInventoryPlanAsync(id, request);
        return Ok(ApiResponse<InventoryPlanDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("inventory/{id}")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> DeleteInventoryPlan(int id)
    {
        await _materialPlanService.DeleteInventoryPlanAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    #endregion

    #region 在产改制计划

    [HttpGet("in-process-rework/{workOrderId}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<InProcessReworkPlanDto>>>> GetInProcessReworkPlans(int workOrderId)
    {
        var result = await _materialPlanService.GetInProcessReworkPlansAsync(workOrderId);
        return Ok(ApiResponse<List<InProcessReworkPlanDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("in-process-rework/detail/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<InProcessReworkPlanDto>>> GetInProcessReworkPlanById(int id)
    {
        var result = await _materialPlanService.GetInProcessReworkPlanByIdAsync(id);
        return Ok(ApiResponse<InProcessReworkPlanDto>.Ok(result, "查询成功"));
    }

    [HttpPost("in-process-rework")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<InProcessReworkPlanDto>>> CreateInProcessReworkPlan(
        [FromBody] CreateInProcessReworkPlanRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<InProcessReworkPlanDto>.Fail("请求参数无效"));

        var result = await _materialPlanService.CreateInProcessReworkPlanAsync(request);
        return Ok(ApiResponse<InProcessReworkPlanDto>.Ok(result, "创建成功"));
    }

    [HttpPut("in-process-rework/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<InProcessReworkPlanDto>>> UpdateInProcessReworkPlan(
        int id, [FromBody] CreateInProcessReworkPlanRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<InProcessReworkPlanDto>.Fail("请求参数无效"));

        var result = await _materialPlanService.UpdateInProcessReworkPlanAsync(id, request);
        return Ok(ApiResponse<InProcessReworkPlanDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("in-process-rework/{id}")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> DeleteInProcessReworkPlan(int id)
    {
        await _materialPlanService.DeleteInProcessReworkPlanAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpGet("in-process-batches/{workOrderId}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<AvailableInProcessBatchDto>>>> GetAvailableInProcessBatches(
        int workOrderId, [FromQuery] ReworkType? reworkType = null, [FromQuery] int? excludePlanId = null)
    {
        var result = await _materialPlanService.GetAvailableInProcessBatchesAsync(workOrderId, reworkType, excludePlanId);
        return Ok(ApiResponse<List<AvailableInProcessBatchDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取所有待处理的在产改制计划（批次上下文通知使用）
    /// </summary>
    [HttpGet("pending-inprocess-rework")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<PendingPlanBatchDto>>>> GetPendingInProcessReworkPlans()
    {
        var result = await _materialPlanService.GetPendingInProcessReworkPlansAsync();
        return Ok(ApiResponse<List<PendingPlanBatchDto>>.Ok(result, "查询成功"));
    }

    #endregion

    #region 在产主工单计划

    [HttpGet("in-main-work-order/{workOrderId}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<InMainWorkOrderPlanDto>>>> GetInMainWorkOrderPlans(int workOrderId)
    {
        var result = await _materialPlanService.GetInMainWorkOrderPlansAsync(workOrderId);
        return Ok(ApiResponse<List<InMainWorkOrderPlanDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("in-main-work-order/detail/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<InMainWorkOrderPlanDto>>> GetInMainWorkOrderPlanById(int id)
    {
        var result = await _materialPlanService.GetInMainWorkOrderPlanByIdAsync(id);
        return Ok(ApiResponse<InMainWorkOrderPlanDto>.Ok(result, "查询成功"));
    }

    [HttpPost("in-main-work-order")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<InMainWorkOrderPlanDto>>> CreateInMainWorkOrderPlan(
        [FromBody] CreateInMainWorkOrderPlanRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<InMainWorkOrderPlanDto>.Fail("请求参数无效"));

        var result = await _materialPlanService.CreateInMainWorkOrderPlanAsync(request);
        return Ok(ApiResponse<InMainWorkOrderPlanDto>.Ok(result, "创建成功"));
    }

    [HttpPut("in-main-work-order/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<InMainWorkOrderPlanDto>>> UpdateInMainWorkOrderPlan(
        int id, [FromBody] CreateInMainWorkOrderPlanRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<InMainWorkOrderPlanDto>.Fail("请求参数无效"));

        var result = await _materialPlanService.UpdateInMainWorkOrderPlanAsync(id, request);
        return Ok(ApiResponse<InMainWorkOrderPlanDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("in-main-work-order/{id}")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> DeleteInMainWorkOrderPlan(int id)
    {
        await _materialPlanService.DeleteInMainWorkOrderPlanAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpGet("main-work-order-batches/{workOrderId}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<AvailableMainWorkOrderBatchDto>>>> GetAvailableMainWorkOrderBatches(
        int workOrderId, [FromQuery] int? excludePlanBatchId = null)
    {
        var result = await _materialPlanService.GetAvailableMainWorkOrderBatchesAsync(workOrderId, excludePlanBatchId);
        return Ok(ApiResponse<List<AvailableMainWorkOrderBatchDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取所有待处理的在产主工单计划（批次上下文通知使用）
    /// </summary>
    [HttpGet("pending-in-main-work-order")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<PendingPlanBatchDto>>>> GetPendingInMainWorkOrderPlans()
    {
        var result = await _materialPlanService.GetPendingInMainWorkOrderPlansAsync();
        return Ok(ApiResponse<List<PendingPlanBatchDto>>.Ok(result, "查询成功"));
    }

    #endregion

    #region 圆棒穿孔计划

    [HttpGet("piercing/{workOrderId}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<RoundBarPiercingPlanDto>>>> GetPiercingPlans(int workOrderId)
    {
        var result = await _materialPlanService.GetPiercingPlansAsync(workOrderId);
        return Ok(ApiResponse<List<RoundBarPiercingPlanDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("piercing/detail/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<RoundBarPiercingPlanDto>>> GetPiercingPlanById(int id)
    {
        var result = await _materialPlanService.GetPiercingPlanByIdAsync(id);
        return Ok(ApiResponse<RoundBarPiercingPlanDto>.Ok(result, "查询成功"));
    }

    [HttpPost("piercing")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<RoundBarPiercingPlanDto>>> CreatePiercingPlan(
        [FromBody] CreateRoundBarPiercingPlanRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<RoundBarPiercingPlanDto>.Fail("请求参数无效"));

        var result = await _materialPlanService.CreatePiercingPlanAsync(request);
        return Ok(ApiResponse<RoundBarPiercingPlanDto>.Ok(result, "创建成功"));
    }

    [HttpPut("piercing/{id}")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<RoundBarPiercingPlanDto>>> UpdatePiercingPlan(
        int id, [FromBody] UpdateRoundBarPiercingPlanRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<RoundBarPiercingPlanDto>.Fail("请求参数无效"));

        var result = await _materialPlanService.UpdatePiercingPlanAsync(id, request);
        return Ok(ApiResponse<RoundBarPiercingPlanDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("piercing/{id}")]
    [Authorize(Roles = $"{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> DeletePiercingPlan(int id)
    {
        await _materialPlanService.DeletePiercingPlanAsync(id);
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

    [HttpGet("print/inventory/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintInventoryPlan(int id)
    {
        var bytes = await _materialPlanService.PrintInventoryPlanAsync(id);
        var base64 = Convert.ToBase64String(bytes);
        return Ok(ApiResponse<string>.Ok(base64, "生成成功"));
    }

    [HttpGet("print/rework/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintReworkPlan(int id)
    {
        var bytes = await _materialPlanService.PrintReworkPlanAsync(id);
        var base64 = Convert.ToBase64String(bytes);
        return Ok(ApiResponse<string>.Ok(base64, "生成成功"));
    }

    [HttpGet("print/piercing/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintPiercingPlan(int id)
    {
        var bytes = await _materialPlanService.PrintPiercingPlanAsync(id);
        var base64 = Convert.ToBase64String(bytes);
        return Ok(ApiResponse<string>.Ok(base64, "生成成功"));
    }

    [HttpGet("print/in-process-rework/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintInProcessReworkPlan(int id)
    {
        var bytes = await _materialPlanService.PrintInProcessReworkPlanAsync(id);
        var base64 = Convert.ToBase64String(bytes);
        return Ok(ApiResponse<string>.Ok(base64, "生成成功"));
    }

    [HttpPost("print/batch")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintBatch([FromBody] MaterialPlanBatchPrintRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));
        if (request.WorkOrderIds.Length == 0)
            return BadRequest(ApiResponse<string>.Fail("请选择工单"));
        if (!request.IncludeSemi && !request.IncludeFinish && !request.IncludeInventory && !request.IncludeRework && !request.IncludeRoundBarPiercing && !request.IncludeInProcessRework && !request.IncludeInMainWorkOrder)
            return BadRequest(ApiResponse<string>.Fail("请至少选择一种计划类型"));

        try
        {
            var bytes = await _materialPlanService.PrintSelectedPlansAsync(request);
            var base64 = Convert.ToBase64String(bytes);
            return Ok(ApiResponse<string>.Ok(base64, "打印生成成功"));
        }
        catch (BusinessException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    [HttpPost("print/semi/{id}/file")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<IActionResult> PrintSemiPlanFile(int id)
    {
        var bytes = await _materialPlanService.PrintSemiPlanAsync(id);
        return File(bytes, "application/pdf", $"荒管采购_{id}.pdf");
    }

    [HttpPost("print/finished/{id}/file")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<IActionResult> PrintFinishedPlanFile(int id)
    {
        var bytes = await _materialPlanService.PrintFinishedPlanAsync(id);
        return File(bytes, "application/pdf", $"成品采购_{id}.pdf");
    }

    [HttpPost("print/inventory/{id}/file")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<IActionResult> PrintInventoryPlanFile(int id)
    {
        var bytes = await _materialPlanService.PrintInventoryPlanAsync(id);
        return File(bytes, "application/pdf", $"库存使用_{id}.pdf");
    }

    [HttpPost("print/rework/{id}/file")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<IActionResult> PrintReworkPlanFile(int id)
    {
        var bytes = await _materialPlanService.PrintReworkPlanAsync(id);
        return File(bytes, "application/pdf", $"库料改制_{id}.pdf");
    }

    [HttpPost("print/piercing/{id}/file")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<IActionResult> PrintPiercingPlanFile(int id)
    {
        var bytes = await _materialPlanService.PrintPiercingPlanAsync(id);
        return File(bytes, "application/pdf", $"圆棒穿孔_{id}.pdf");
    }

    [HttpPost("print/in-process-rework/{id}/file")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<IActionResult> PrintInProcessReworkPlanFile(int id)
    {
        var bytes = await _materialPlanService.PrintInProcessReworkPlanAsync(id);
        return File(bytes, "application/pdf", $"在产改制_{id}.pdf");
    }

    [HttpGet("print/in-main-work-order/{id}")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintInMainWorkOrderPlan(int id)
    {
        var bytes = await _materialPlanService.PrintInMainWorkOrderPlanAsync(id);
        var base64 = Convert.ToBase64String(bytes);
        return Ok(ApiResponse<string>.Ok(base64, "生成成功"));
    }

    [HttpPost("print/in-main-work-order/{id}/file")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<IActionResult> PrintInMainWorkOrderPlanFile(int id)
    {
        var bytes = await _materialPlanService.PrintInMainWorkOrderPlanAsync(id);
        return File(bytes, "application/pdf", $"在产主工单_{id}.pdf");
    }

    #endregion

    #region 仓库通知

    /// <summary>
    /// 获取指定仓库中存在未出库用料计划的批次列表
    /// </summary>
    [HttpGet("pending-batches/{warehouseId}")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<PendingPlanBatchDto>>>> GetPendingPlanBatches(int warehouseId)
    {
        var result = await _materialPlanService.GetPendingPlanBatchesByWarehouseAsync(warehouseId);
        return Ok(ApiResponse<List<PendingPlanBatchDto>>.Ok(result, "查询成功"));
    }

    #endregion
}
