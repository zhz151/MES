using MES.Core.DTOs.Warehouse;
using MES.Core.Models;

namespace MES.Core.Interfaces.Warehouse;

/// <summary>
/// 出库记录写操作服务
/// </summary>
public interface IOutboundWriteService
{
    /// <summary>
    /// 出库
    /// </summary>
    Task<OutboundRecordDto> OutboundAsync(CreateOutboundRequest request);

    /// <summary>
    /// 批量出库
    /// </summary>
    Task<BatchOutboundResult> BatchOutboundAsync(BatchOutboundRequest request);

    /// <summary>
    /// 更新出库记录
    /// </summary>
    Task<OutboundRecordDto> UpdateOutboundRecordAsync(long id, UpdateOutboundRecordRequest request);

    /// <summary>
    /// 物理删除出库记录
    /// </summary>
    Task HardDeleteOutboundRecordAsync(long id);
}
