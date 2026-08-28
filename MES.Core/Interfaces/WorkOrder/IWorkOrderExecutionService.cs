using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.WorkOrder;
namespace MES.Core.Interfaces.WorkOrder;

/// <summary>
/// 工单执行状况服务接口（只读查询 + 手动刷新）
/// </summary>
public interface IWorkOrderExecutionService
{
    /// <summary>
    /// 分页查询工单执行状况
    /// </summary>
    Task<PagedResult<WorkOrderExecutionSummaryDto>> GetPagedAsync(QueryParams query, DateTime? signDateFrom = null, DateTime? signDateTo = null, DateTime? deliveryDateStart = null, DateTime? deliveryDateEnd = null);

    /// <summary>
    /// 全量刷新所有工单的执行状况汇总数据
    /// </summary>
    Task<WorkOrderExecutionRefreshResultDto> RefreshAllAsync();

    /// <summary>
    /// 获取筛选上下文（各列的筛选项列表）
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>
    /// 获取工单执行看板聚合数据（按 ScheduleStage × UrgencyLevel 分组）
    /// </summary>
    Task<List<WorkOrderExecutionDashboardItem>> GetDashboardSummaryAsync();

    /// <summary>
    /// 获取「错误疑问投料」明细：到料实投一致性 ∈ {2,3,4,5}（2 疑问-到料少投/3 疑问-到料超投/4 错误-无料已投/5 错误-无需投料）的全量工单行
    /// </summary>
    Task<List<ErrorDoubtInputItemDto>> GetErrorDoubtInputItemsAsync();

    /// <summary>
    /// 获取「在产在检-错疑待料」聚合：主号-关注 = 1 主号完成 / 3 生产执行 / 4 成品检验 三档，
    /// 分别统计「理论原料未至」（TotalMissingWeight &gt; 0）与「工单到料未投」（PendingInputWeight &gt; 0）的工单数 + 累计重量
    /// </summary>
    Task<List<InProductionInspectionDoubtItemDto>> GetInProductionInspectionDoubtItemsAsync();

    /// <summary>
    /// 增量刷新指定工单号的执行状况汇总
    /// </summary>
    Task RefreshByWorkOrderNosAsync(List<string> workOrderNos);

    /// <summary>按筛选条件打印全部数据</summary>
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, DateTime? signDateFrom, DateTime? signDateTo, DateTime? deliveryDateStart, DateTime? deliveryDateEnd, List<PrintColumnDef> columns);

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns);
}
