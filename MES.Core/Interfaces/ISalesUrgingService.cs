using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 销售催单服务接口
/// </summary>
public interface ISalesUrgingService
{
    /// <summary>分页查询（G1+G12 + LEFT JOIN SalesUrging）</summary>
    Task<PagedResult<SalesUrgingDto>> GetPagedAsync(QueryParams query);

    /// <summary>保存销售催单（upsert）</summary>
    Task<bool> SaveUrgingAsync(int workOrderId, bool isSalesUrging, string? urgingRemark);
}
