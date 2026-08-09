using MES.Core.DTOs.Warehouse;
using MES.Core.Models;
using MES.Core.DTOs.Batch;

namespace MES.Core.Interfaces.Warehouse;

/// <summary>
/// 入库批次写操作服务
/// </summary>
public interface IInventoryBatchWriteService
{
    /// <summary>
    /// 获取单个批次详情
    /// </summary>
    Task<InventoryBatchDto> GetByIdAsync(int id);

    /// <summary>
    /// 入库
    /// </summary>
    Task<InventoryBatchDto> InboundAsync(CreateInboundRequest request);

    /// <summary>
    /// 批量入库
    /// </summary>
    Task<BatchInboundResult> BatchInboundAsync(BatchInboundRequest request);

    /// <summary>
    /// 更新入库批次
    /// </summary>
    Task<InventoryBatchDto> UpdateInventoryBatchAsync(int id, UpdateInventoryBatchRequest request);

    /// <summary>
    /// 物理删除入库批次
    /// </summary>
    Task HardDeleteInventoryBatchAsync(int id);

    /// <summary>
    /// 全量回填定尺切割长度匹配标识，返回更新条数
    /// </summary>
    Task<int> RefreshAllCutLengthMatchAsync();
}
