using MES.Core.Models;

using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.Batch;
namespace MES.Core.Interfaces.Warehouse;

public interface IInventoryService
{
    /// <summary>
    /// 分页查询库存批次
    /// </summary>
    Task<PagedResult<InventoryBatchDto>> GetPagedAsync(InventoryQueryParams query);

    /// <summary>
    /// 全量查询库存批次（无分页，供前端 Items 模式使用�?    /// </summary>
    Task<List<InventoryBatchDto>> GetAllListAsync(InventoryQueryParams query);

    /// <summary>
    /// 获取单个批次详情
    /// </summary>
    Task<InventoryBatchDto> GetByIdAsync(int id);

    /// <summary>
    /// 批量入库
    /// </summary>
    Task<BatchInboundResult> BatchInboundAsync(BatchInboundRequest request);

    /// <summary>
    /// 入库
    /// </summary>
    Task<InventoryBatchDto> InboundAsync(CreateInboundRequest request);

    /// <summary>
    /// 出库
    /// </summary>
    Task<OutboundRecordDto> OutboundAsync(CreateOutboundRequest request);

    /// <summary>
    /// 批量出库
    /// </summary>
    Task<BatchOutboundResult> BatchOutboundAsync(BatchOutboundRequest request);

    /// <summary>
    /// 查询出库记录
    /// </summary>
    Task<PagedResult<OutboundRecordDto>> GetOutboundRecordsAsync(OutboundQueryParams query);

    /// <summary>
    /// 更新入库批次
    /// </summary>
    Task<InventoryBatchDto> UpdateInventoryBatchAsync(int id, UpdateInventoryBatchRequest request);

    /// <summary>
    /// 物理删除入库批次（仅管理�?主任�?    /// </summary>
    Task HardDeleteInventoryBatchAsync(int id);

    /// <summary>
    /// 更新出库记录
    /// </summary>
    Task<OutboundRecordDto> UpdateOutboundRecordAsync(long id, UpdateOutboundRecordRequest request);

    /// <summary>
    /// 物理删除出库记录（仅管理�?主任�?    /// </summary>
    Task HardDeleteOutboundRecordAsync(long id);

    /// <summary>
    /// 验证来源单号
    /// </summary>
    Task<SourceOrderValidationResult> ValidateSourceOrderAsync(string sourceOrderNo, string inboundSource, int? sourceOrderSequence = null);

    /// <summary>
    /// 验证生产批号（检验入库自动填充）
    /// </summary>
    Task<SourceOrderValidationResult> ValidateProductionBatchAsync(string productionBatchNo);

    /// <summary>
    /// 验证仓库内入库数据的工单号是否在工单管理上下文中存在
    /// </summary>
    Task<List<string>> ValidateWarehouseWorkOrderNosAsync(int warehouseId);

    /// <summary>
    /// 获取入库批次中工单号不存在的批次列表（实时扫描）
    /// </summary>
    Task<List<BatchWorkOrderMismatchDto>> GetMismatchedWorkOrderBatchesAsync(int? warehouseId = null);

    /// <summary>
    /// 获取仓库入库批次中引用的所有工单号（用于过滤工单变更通知）
    /// </summary>
    Task<List<string>> GetDistinctWorkOrderNosByWarehouseAsync(int warehouseId);

    // ========== 打印 ==========

    /// <summary>
    /// 打印全部库存/入库记录
    /// </summary>
    Task<byte[]> PrintInventoryAllAsync(InventoryPrintAllRequest request);

    /// <summary>
    /// 打印选中库存/入库记录
    /// </summary>
    Task<byte[]> PrintInventorySelectedAsync(InventoryPrintSelectedRequest request);

    /// <summary>
    /// 打印全部库存（只显示有库存的记录�?    /// </summary>
    Task<byte[]> PrintStockAllAsync(InventoryPrintAllRequest request);

    /// <summary>
    /// 打印选中库存批次
    /// </summary>
    Task<byte[]> PrintStockSelectedAsync(InventoryPrintSelectedRequest request);

    /// <summary>
    /// 打印全部入库历史
    /// </summary>
    Task<byte[]> PrintInboundAllAsync(InventoryPrintAllRequest request);

    /// <summary>
    /// 打印选中入库批次
    /// </summary>
    Task<byte[]> PrintInboundSelectedAsync(InventoryPrintSelectedRequest request);

    /// <summary>
    /// 打印全部出库记录
    /// </summary>
    Task<byte[]> PrintOutboundAllAsync(OutboundPrintAllRequest request);

    /// <summary>
    /// 打印选中出库记录
    /// </summary>
    Task<byte[]> PrintOutboundSelectedAsync(OutboundPrintSelectedRequest request);

    /// <summary>
    /// 获取出库记录筛选上下文（各列去重值），用�?ExcelFilter 下拉选项
    /// </summary>
    Task<Dictionary<string, List<string>>> GetOutboundFilterContextsAsync();

    /// <summary>
    /// 获取库存批次筛选上下文（各列去重值），用�?ExcelFilter 下拉选项
    /// </summary>
    Task<Dictionary<string, List<string>>> GetInventoryFilterContextsAsync();

    /// <summary>
    /// 月度库存变化汇总（行=材料，列=期初+12月入/出/结+合计；结存为真实全口径，入/出按来源/类型 5 分）
    /// </summary>
    Task<MonthlyStockSummaryResultDto> GetMonthlyStockSummaryAsync();
}
