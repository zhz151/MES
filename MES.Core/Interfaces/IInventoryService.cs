using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IInventoryService
{
    /// <summary>
    /// 分页查询库存批次
    /// </summary>
    Task<PagedResult<InventoryBatchDto>> GetPagedAsync(InventoryQueryParams query);

    /// <summary>
    /// 全量查询库存批次（无分页，供前端 Items 模式使用）
    /// </summary>
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
    /// 物理删除入库批次（仅管理员/主任）
    /// </summary>
    Task HardDeleteInventoryBatchAsync(int id);

    /// <summary>
    /// 更新出库记录
    /// </summary>
    Task<OutboundRecordDto> UpdateOutboundRecordAsync(long id, UpdateOutboundRecordRequest request);

    /// <summary>
    /// 物理删除出库记录（仅管理员/主任）
    /// </summary>
    Task HardDeleteOutboundRecordAsync(long id);

    /// <summary>
    /// 验证来源单号
    /// </summary>
    Task<SourceOrderValidationResult> ValidateSourceOrderAsync(string sourceOrderNo, string inboundSource, int? sourceOrderSequence = null);

    /// <summary>
    /// 验证仓库内入库数据的工单号是否在工单管理上下文中存在
    /// </summary>
    Task<List<string>> ValidateWarehouseWorkOrderNosAsync(int warehouseId);

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
    /// 打印全部出库记录
    /// </summary>
    Task<byte[]> PrintOutboundAllAsync(OutboundPrintAllRequest request);

    /// <summary>
    /// 打印选中出库记录
    /// </summary>
    Task<byte[]> PrintOutboundSelectedAsync(OutboundPrintSelectedRequest request);

    /// <summary>
    /// 获取出库记录筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    Task<Dictionary<string, List<string>>> GetOutboundFilterContextsAsync();

    /// <summary>
    /// 获取库存批次筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    Task<Dictionary<string, List<string>>> GetInventoryFilterContextsAsync();
}
