using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 订单需求调整服务接口
/// </summary>
public interface IOrderDemandAdjustmentService
{
    /// <summary>分页查询（G1+G12 + LEFT JOIN OrderDemandAdjustment）</summary>
    Task<PagedResult<OrderDemandAdjustmentDto>> GetPagedAsync(QueryParams query);

    /// <summary>保存订单需求调整（upsert）</summary>
    Task<bool> SaveUrgingAsync(int workOrderId, bool isUrging, bool isBatchDelivery, bool isPaused, string? adjustmentRemark);

    /// <summary>获取筛选上下文（各列去重值），用于 ExcelFilter 下拉选项</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
