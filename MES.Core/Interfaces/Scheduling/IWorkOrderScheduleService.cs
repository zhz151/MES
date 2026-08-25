using MES.Core.Models;

using MES.Core.DTOs.WorkOrder;
using MES.Core.DTOs.Scheduling;
namespace MES.Core.Interfaces.Scheduling;

/// <summary>
/// 工单排程服务接口
/// </summary>
public interface IWorkOrderScheduleService
{
    /// <summary>分页查询（WorkOrderExecutionSummary LEFT JOIN WorkOrderPlan）
    /// </summary>
    Task<PagedResult<WorkOrderScheduleDto>> GetPagedAsync(QueryParams query);

    /// <summary>获取筛选上下文</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>保存工单计划薄表覆盖值
    /// </summary>
    Task<bool> SavePlanAsync(SaveWorkOrderPlanRequest request);

    /// <summary>批量计划安排：将匹配查询的工单Plan覆盖值设为系统值，删除不匹配的Plan。
    /// </summary>
    Task<bool> PlanScheduleAllAsync(QueryParams query);

    /// <summary>进度调整保留计划：与全量计划相同，但实时一致性为「进度调整」的工单其薄表「生产关注」字段保留不覆盖，删除不匹配的Plan。
    /// </summary>
    Task<bool> PlanScheduleKeepAdjustmentAsync(QueryParams query);

    /// <summary>获取所有工单排程（无分页，供看板使用）</summary>
    Task<List<WorkOrderScheduleDto>> GetAllAsync();

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<MES.Core.DTOs.Shared.PrintColumnDef> columns);
}
