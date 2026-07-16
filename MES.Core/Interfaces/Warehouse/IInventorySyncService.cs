using MES.Core.DTOs.Order;
using MES.Core.DTOs.Batch;

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
    /// 验证仓库内入库数据的工单号是否在工单管理上下文中存在
    /// </summary>
    Task<List<string>> ValidateWarehouseWorkOrderNosAsync(int warehouseId);

    /// <summary>
    /// 获取入库批次中工单号不存在的批次列表
    /// </summary>
    Task<List<BatchWorkOrderMismatchDto>> GetMismatchedWorkOrderBatchesAsync(int? warehouseId = null);

    /// <summary>
    /// 入库批次变更后自动同步采购单/委外单的收货数量及状态
    /// </summary>
    Task SyncSourceOrdersAsync(List<string> sourceOrderNos);
}
