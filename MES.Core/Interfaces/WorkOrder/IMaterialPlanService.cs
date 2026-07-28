using MES.Core.Enums;
using MES.Core.Models;

using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.WorkOrder;
using MES.Core.DTOs.Batch;
namespace MES.Core.Interfaces.WorkOrder;

/// <summary>
/// 用料计划服务接口
/// </summary>
public interface IMaterialPlanService
{
    // ========== 原料采购计划 ==========

    /// <summary>
    /// 获取工单的原料采购计划列�?    /// </summary>
    Task<List<PurchaseSemiPlanDto>> GetSemiPlansAsync(int workOrderId);

    /// <summary>
    /// 获取单个原料采购计划详情
    /// </summary>
    Task<PurchaseSemiPlanDto> GetSemiPlanByIdAsync(int id);

    /// <summary>
    /// 创建原料采购计划（含测算�?    /// </summary>
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
    /// 获取工单的成品采购计划列�?    /// </summary>
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
    /// 获取工单的库存使用计划列�?    /// </summary>
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
    /// 批量创建库存使用计划（含改制计划�?    /// </summary>
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
    /// 获取工单的改制计划列�?    /// </summary>
    Task<List<InventoryPlanDto>> GetReworkPlansAsync(int workOrderId);

    // ========== 用料测算 ==========

    /// <summary>
    /// 执行用料测算（前端传参，后端计算�?    /// </summary>
    Task<MaterialCalculateResult> CalculateAsync(MaterialCalculateRequest request);

    // ========== 计划状�?==========

    /// <summary>
    /// 获取工单的用料计划汇�?    /// </summary>
    Task<WorkOrderMaterialPlanDto> GetWorkOrderMaterialPlanAsync(int workOrderId);

    /// <summary>
    /// 刷新工单用料计划状�?    /// </summary>
    Task UpdateMaterialPlanStatusAsync(int workOrderId);

    // ========== 圆棒穿孔计划 ==========

    /// <summary>
    /// 获取工单的圆棒穿孔计划列�?    /// </summary>
    Task<List<RoundBarPiercingPlanDto>> GetPiercingPlansAsync(int workOrderId);

    /// <summary>
    /// 获取单个圆棒穿孔计划详情
    /// </summary>
    Task<RoundBarPiercingPlanDto> GetPiercingPlanByIdAsync(int id);

    /// <summary>
    /// 创建圆棒穿孔计划（含测算�?    /// </summary>
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
    /// 生成原料采购申请单PDF（返回byte[]�?    /// </summary>
    Task<byte[]> PrintSemiPlanAsync(int planId);

    /// <summary>
    /// 生成成品采购申请单PDF（返回byte[]�?    /// </summary>
    Task<byte[]> PrintFinishedPlanAsync(int planId);

    /// <summary>
    /// 生成库存使用单PDF（返回byte[]�?    /// </summary>
    Task<byte[]> PrintInventoryPlanAsync(int planId);

    /// <summary>
    /// 生成库料改制单PDF（返回byte[]�?    /// </summary>
    Task<byte[]> PrintReworkPlanAsync(int planId);

    /// <summary>
    /// 生成圆棒穿孔计划PDF（返回byte[]�?    /// </summary>
    Task<byte[]> PrintPiercingPlanAsync(int planId);

    /// <summary>
    /// 生成在产改制计划PDF（返回byte[]）    /// </summary>
    Task<byte[]> PrintInProcessReworkPlanAsync(int planId);

    /// <summary>
    /// 生成在产主工单计划PDF（返回byte[]）    /// </summary>
    Task<byte[]> PrintInMainWorkOrderPlanAsync(int planId);

    /// <summary>
    /// 批量打印选中工单的指定类型用料计划    /// </summary>
    Task<byte[]> PrintSelectedPlansAsync(MaterialPlanBatchPrintRequest request);

    /// <summary>
    /// 重新计算库料改制计划的工艺周期（工序组变更后调用�?    /// </summary>
    Task RecalculateStandardCycleForBatchAsync(string batchNo);

    // ========== 在产改制计划 ==========

    /// <summary>
    /// 获取工单的在产改制计划列�?    /// </summary>
    Task<List<InProcessReworkPlanDto>> GetInProcessReworkPlansAsync(int workOrderId);

    /// <summary>
    /// 获取单个在产改制计划详情
    /// </summary>
    Task<InProcessReworkPlanDto> GetInProcessReworkPlanByIdAsync(int id);

    /// <summary>
    /// 创建在产改制计划
    /// </summary>
    Task<InProcessReworkPlanDto> CreateInProcessReworkPlanAsync(CreateInProcessReworkPlanRequest request);

    /// <summary>
    /// 更新在产改制计划
    /// </summary>
    Task<InProcessReworkPlanDto> UpdateInProcessReworkPlanAsync(int id, CreateInProcessReworkPlanRequest request);

    /// <summary>
    /// 删除在产改制计划
    /// </summary>
    Task DeleteInProcessReworkPlanAsync(int id);

    /// <summary>
    /// 获取工单可用的在产批次（非工�?+ 未产/在产�?    /// </summary>
    Task<List<AvailableInProcessBatchDto>> GetAvailableInProcessBatchesAsync(int workOrderId, ReworkType? reworkType = null, int? excludePlanId = null);

    // ========== 在产主工单计划 ==========

    /// <summary>
    /// 获取工单的在产主工单计划列表
    /// </summary>
    Task<List<InMainWorkOrderPlanDto>> GetInMainWorkOrderPlansAsync(int workOrderId);

    /// <summary>
    /// 获取单个在产主工单计划详情
    /// </summary>
    Task<InMainWorkOrderPlanDto> GetInMainWorkOrderPlanByIdAsync(int id);

    /// <summary>
    /// 创建在产主工单计划
    /// </summary>
    Task<InMainWorkOrderPlanDto> CreateInMainWorkOrderPlanAsync(CreateInMainWorkOrderPlanRequest request);

    /// <summary>
    /// 更新在产主工单计划
    /// </summary>
    Task<InMainWorkOrderPlanDto> UpdateInMainWorkOrderPlanAsync(int id, CreateInMainWorkOrderPlanRequest request);

    /// <summary>
    /// 删除在产主工单计划
    /// </summary>
    Task DeleteInMainWorkOrderPlanAsync(int id);

    /// <summary>
    /// 获取工单可用的主工单批次（用于创建在产主工单计划）
    /// </summary>
    Task<List<AvailableMainWorkOrderBatchDto>> GetAvailableMainWorkOrderBatchesAsync(int workOrderId, int? excludePlanBatchId = null);

    /// <summary>
    /// 获取所有待处理（Planned状态）的在产主工单计划列表
    /// </summary>
    Task<List<PendingPlanBatchDto>> GetPendingInMainWorkOrderPlansAsync();

    /// <summary>
    /// 根据批次ID消除所有待处理的在产主工单计划通知（有效量变更时触发）
    /// </summary>
    Task DismissInMainWorkOrderPlansByBatchAsync(int productionBatchId);

    // ========== 批次通知 ==========

    /// <summary>
    /// 获取所有待处理（Planned状态）的在产改制计划列�?    /// </summary>
    Task<List<PendingPlanBatchDto>> GetPendingInProcessReworkPlansAsync();

    // ========== 仓库通知 ==========

    /// <summary>
    /// 获取指定仓库中存在未出库用料计划的批次列�?    /// </summary>
    Task<List<PendingPlanBatchDto>> GetPendingPlanBatchesByWarehouseAsync(int warehouseId);
}
