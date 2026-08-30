using MES.Core.DTOs.Order;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Warehouse;

namespace MES.Core.Interfaces.Warehouse;

/// <summary>
/// 库存联动同步服务（来源单验证/同步、工单号校验）
/// </summary>
public interface IInventorySyncService
{
    /// <summary>
    /// 验证来源单号
    /// </summary>
    Task<SourceOrderValidationResult> ValidateSourceOrderAsync(string sourceOrderNo, string inboundSource, int? sourceOrderSequence = null);

    /// <summary>
    /// 验证生产批号（检验入库自动填充）
    /// </summary>
    Task<SourceOrderValidationResult> ValidateProductionBatchAsync(string productionBatchNo);

    /// <summary>
    /// 按入库批次来源（采购单号/委外单号+序号/生产批号）解析应关联的工单号+订单号+主号
    /// （入库更正页点击「关联工单=是」时即时回填）
    /// </summary>
    Task<SourceOrderValidationResult> ResolveLinkedWorkOrderAsync(int inventoryBatchId);

    /// <summary>
    /// 获取入库批次中工单号不存在的批次列表
    /// </summary>
    Task<List<BatchWorkOrderMismatchDto>> GetMismatchedWorkOrderBatchesAsync(int? warehouseId = null);

    /// <summary>
    /// 获取来源单号关联工单号已变更的入库批次列表（实时扫描：比对来源单当前工单号与批次冗余工单号）
    /// </summary>
    Task<List<SourceOrderChangedBatchDto>> GetSourceOrderChangedBatchesAsync(int? warehouseId = null);

    /// <summary>
    /// 获取仓库入库批次中引用的所有工单号（用于过滤工单变更通知）
    /// </summary>
    Task<List<string>> GetDistinctWorkOrderNosByWarehouseAsync(int warehouseId);

    /// <summary>
    /// 入库批次变更后自动同步采购单/委外单的收货数量及状态
    /// </summary>
    Task SyncSourceOrdersAsync(List<string> sourceOrderNos);
}
