using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 工单排程服务接口（LEFT JOIN 实时查询模式）
/// </summary>
public interface IWorkOrderScheduleService
{
    /// <summary>分页查询（WorkOrderExecutionSummary LEFT JOIN OrderDemandAdjustment）</summary>
    Task<PagedResult<WorkOrderScheduleDto>> GetPagedAsync(QueryParams query);

    /// <summary>获取筛选上下文</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
