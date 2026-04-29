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
}
