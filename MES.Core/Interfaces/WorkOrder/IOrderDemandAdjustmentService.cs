using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.WorkOrder;
namespace MES.Core.Interfaces.WorkOrder;

/// <summary>
/// 工单需求调整服务接口
/// </summary>
public interface IOrderDemandAdjustmentService
{
    /// <summary>分页查询（G1+G12 + LEFT JOIN OrderDemandAdjustment）</summary>
    Task<PagedResult<OrderDemandAdjustmentDto>> GetPagedAsync(QueryParams query, DateTime? signDateFrom = null, DateTime? signDateTo = null);

    /// <summary>保存工单需求调整（upsert）</summary>
    Task<bool> SaveUrgingAsync(int workOrderId, bool isUrging, bool isBatchDelivery, bool isPaused, string? adjustmentRemark);

    /// <summary>获取筛选上下文（各列去重值），用于 ExcelFilter 下拉选项</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>按筛选条件打印全部数据</summary>
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, DateTime? signDateFrom, DateTime? signDateTo, List<PrintColumnDef> columns);

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns);
}
