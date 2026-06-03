using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 工单排程服务接口
/// </summary>
public interface IWorkOrderScheduleService
{
    /// <summary>分页查询</summary>
    Task<PagedResult<WorkOrderScheduleDto>> GetPagedAsync(QueryParams query);

    /// <summary>计划安排：全量删除+插入（从 WorkOrderExecutionSummary 重新获取符合条件的工单）</summary>
    Task<int> PlanArrangementAsync();

    /// <summary>执行数据更新：刷新 G7/G12 字段</summary>
    Task<int> ExecuteDataUpdateAsync();

    /// <summary>获取筛选上下文</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
