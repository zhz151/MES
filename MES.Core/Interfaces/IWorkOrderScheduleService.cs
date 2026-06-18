using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 工单排程服务接口
/// </summary>
public interface IWorkOrderScheduleService
{
    /// <summary>分页查询（WorkOrderExecutionSummary LEFT JOIN WorkOrderPlan）</summary>
    Task<PagedResult<WorkOrderScheduleDto>> GetPagedAsync(QueryParams query);

    /// <summary>获取筛选上下文</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>保存工单计划薄表覆盖值</summary>
    Task<bool> SavePlanAsync(SaveWorkOrderPlanRequest request);

    /// <summary>批量计划安排：将匹配查询的工单Plan覆盖值设为系统值，删除不匹配的Plan行</summary>
    Task<bool> PlanScheduleAllAsync(QueryParams query);

    /// <summary>获取所有工单排程（无分页，供看板使用）</summary>
    Task<List<WorkOrderScheduleDto>> GetAllAsync();
}
