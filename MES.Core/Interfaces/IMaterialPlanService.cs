using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 用料计划服务接口
/// </summary>
public interface IMaterialPlanService
{
    // ========== 原料采购计划 ==========

    /// <summary>
    /// 获取工单的原料采购计划列表
    /// </summary>
    Task<List<PurchaseSemiPlanDto>> GetSemiPlansAsync(int workOrderId);

    /// <summary>
    /// 获取单个原料采购计划详情
    /// </summary>
    Task<PurchaseSemiPlanDto> GetSemiPlanByIdAsync(int id);

    /// <summary>
    /// 创建原料采购计划（含测算）
    /// </summary>
    Task<PurchaseSemiPlanDto> CreateSemiPlanAsync(CreatePurchaseSemiPlanRequest request);

    /// <summary>
    /// 更新原料采购计划
    /// </summary>
    Task<PurchaseSemiPlanDto> UpdateSemiPlanAsync(int id, CreatePurchaseSemiPlanRequest request);

    /// <summary>
    /// 删除原料采购计划
    /// </summary>
    Task DeleteSemiPlanAsync(int id);

    // ========== 成品采购计划 ==========

    /// <summary>
    /// 获取工单的成品采购计划列表
    /// </summary>
    Task<List<PurchaseFinishedPlanDto>> GetFinishedPlansAsync(int workOrderId);

    /// <summary>
    /// 获取单个成品采购计划详情
    /// </summary>
    Task<PurchaseFinishedPlanDto> GetFinishedPlanByIdAsync(int id);

    /// <summary>
    /// 创建成品采购计划
    /// </summary>
    Task<PurchaseFinishedPlanDto> CreateFinishedPlanAsync(CreatePurchaseFinishedPlanRequest request);

    /// <summary>
    /// 批量创建成品采购计划
    /// </summary>
    Task<List<PurchaseFinishedPlanDto>> CreateFinishedPlanBatchAsync(List<CreatePurchaseFinishedPlanRequest> requests);

    /// <summary>
    /// 更新成品采购计划
    /// </summary>
    Task<PurchaseFinishedPlanDto> UpdateFinishedPlanAsync(int id, CreatePurchaseFinishedPlanRequest request);

    /// <summary>
    /// 删除成品采购计划
    /// </summary>
    Task DeleteFinishedPlanAsync(int id);

    // ========== 库存使用计划 ==========

    /// <summary>
    /// 获取工单的库存使用计划列表
    /// </summary>
    Task<List<InventoryPlanDto>> GetInventoryPlansAsync(int workOrderId);

    /// <summary>
    /// 获取库存使用计划详情
    /// </summary>
    Task<InventoryPlanDto> GetInventoryPlanByIdAsync(int id);

    /// <summary>
    /// 创建库存使用计划
    /// </summary>
    Task<InventoryPlanDto> CreateInventoryPlanAsync(CreateInventoryPlanRequest request);

    /// <summary>
    /// 批量创建库存使用计划（含改制计划）
    /// </summary>
    Task<List<InventoryPlanDto>> CreateInventoryPlanBatchAsync(List<CreateInventoryPlanRequest> requests);

    /// <summary>
    /// 更新库存使用计划
    /// </summary>
    Task<InventoryPlanDto> UpdateInventoryPlanAsync(int id, CreateInventoryPlanRequest request);

    /// <summary>
    /// 删除库存使用计划
    /// </summary>
    Task DeleteInventoryPlanAsync(int id);

    /// <summary>
    /// 获取工单可用库存批次列表
    /// </summary>
    Task<List<AvailableInventoryBatchDto>> GetAvailableInventoryAsync(int workOrderId, int? excludePlanId = null);

    /// <summary>
    /// 获取工单可用改制库存（根据改制类型筛选）
    /// </summary>
    Task<List<AvailableInventoryBatchDto>> GetAvailableReworkInventoryAsync(int workOrderId, ReworkType reworkType, int? excludePlanId = null);

    /// <summary>
    /// 获取工单的改制计划列表
    /// </summary>
    Task<List<InventoryPlanDto>> GetReworkPlansAsync(int workOrderId);

    // ========== 用料测算 ==========

    /// <summary>
    /// 执行用料测算（前端传参，后端计算）
    /// </summary>
    Task<MaterialCalculateResult> CalculateAsync(MaterialCalculateRequest request);

    // ========== 计划状态 ==========

    /// <summary>
    /// 获取工单的用料计划汇总
    /// </summary>
    Task<WorkOrderMaterialPlanDto> GetWorkOrderMaterialPlanAsync(int workOrderId);

    /// <summary>
    /// 刷新工单用料计划状态
    /// </summary>
    Task UpdateMaterialPlanStatusAsync(int workOrderId);

    // ========== 圆棒穿孔计划 ==========

    /// <summary>
    /// 获取工单的圆棒穿孔计划列表
    /// </summary>
    Task<List<RoundBarPiercingPlanDto>> GetPiercingPlansAsync(int workOrderId);

    /// <summary>
    /// 获取单个圆棒穿孔计划详情
    /// </summary>
    Task<RoundBarPiercingPlanDto> GetPiercingPlanByIdAsync(int id);

    /// <summary>
    /// 创建圆棒穿孔计划（含测算）
    /// </summary>
    Task<RoundBarPiercingPlanDto> CreatePiercingPlanAsync(CreateRoundBarPiercingPlanRequest request);

    /// <summary>
    /// 更新圆棒穿孔计划
    /// </summary>
    Task<RoundBarPiercingPlanDto> UpdatePiercingPlanAsync(int id, UpdateRoundBarPiercingPlanRequest request);

    /// <summary>
    /// 删除圆棒穿孔计划
    /// </summary>
    Task DeletePiercingPlanAsync(int id);

    // ========== 打印 ==========

    /// <summary>
    /// 生成原料采购申请单PDF（返回byte[]）
    /// </summary>
    Task<byte[]> PrintSemiPlanAsync(int planId);

    /// <summary>
    /// 生成成品采购申请单PDF（返回byte[]）
    /// </summary>
    Task<byte[]> PrintFinishedPlanAsync(int planId);

    /// <summary>
    /// 生成库存使用单PDF（返回byte[]）
    /// </summary>
    Task<byte[]> PrintInventoryPlanAsync(int planId);

    /// <summary>
    /// 生成库料改制单PDF（返回byte[]）
    /// </summary>
    Task<byte[]> PrintReworkPlanAsync(int planId);

    /// <summary>
    /// 生成圆棒穿孔计划PDF（返回byte[]）
    /// </summary>
    Task<byte[]> PrintPiercingPlanAsync(int planId);

    /// <summary>
    /// 批量打印选中工单的指定类型用料计划
    /// </summary>
    Task<byte[]> PrintSelectedPlansAsync(MaterialPlanBatchPrintRequest request);
}
