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

    /// <summary>保存原料锁定确认（预计到料日期+齐全标记+确认锁定）</summary>
    Task<bool> SaveLockConfirmationAsync(int workOrderId, DateTime? estimatedArrivalDate, bool isMainNoMaterialComplete);

    /// <summary>取消锁定（回退到原料锁定状态）</summary>
    Task<bool> UnlockAsync(int workOrderId);

    /// <summary>获取筛选上下文（各列去重值），用于 ExcelFilter 下拉选项</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
