using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 工单执行状况服务接口（只读查询 + 手动刷新）
/// </summary>
public interface IWorkOrderExecutionService
{
    /// <summary>
    /// 分页查询工单执行状况
    /// </summary>
    Task<PagedResult<WorkOrderExecutionSummaryDto>> GetPagedAsync(QueryParams query);

    /// <summary>
    /// 全量刷新所有工单的执行状况汇总数据
    /// </summary>
    Task<WorkOrderExecutionRefreshResultDto> RefreshAllAsync();

    /// <summary>
    /// 获取筛选上下文（各列的筛选项列表）
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
