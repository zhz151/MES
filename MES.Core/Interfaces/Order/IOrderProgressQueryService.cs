using MES.Core.DTOs.Order;

namespace MES.Core.Interfaces.Order;

/// <summary>
/// 订单进度树查询服务（只读）：按订单号聚合为「主号 → 四阶段分支 → 重量叶」的树。
/// 数据源均复用既有口径：主号枚举/原料锁定/生产执行取 WorkOrderExecutionSummary 快照，
/// 成品检验取成检计划看板（GetKanbanAsync），成品入库取 InventoryBatch/OutboundRecords 实时。
/// </summary>
public interface IOrderProgressQueryService
{
    /// <summary>
    /// 获取指定订单号的进度树；无该订单工单时返回 null
    /// </summary>
    Task<OrderProgressTreeDto?> GetTreeAsync(string salesOrderNo);
}
