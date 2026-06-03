using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 原锁计划及执行服务接口
/// </summary>
public interface IRawMaterialLockPlanAndExecutionService
{
    /// <summary>分页查询</summary>
    Task<PagedResult<RawMaterialLockPlanAndExecutionDto>> GetPagedAsync(QueryParams query);

    /// <summary>计划安排：全量删除+插入（从 WorkOrderExecutionSummary + SalesUrging 重新获取）</summary>
    Task<int> PlanArrangementAsync();

    /// <summary>执行数据更新：仅刷新 G14 快照字段</summary>
    Task<int> ExecuteDataUpdateAsync();

    /// <summary>获取筛选上下文</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>批量设置预执行标记（执行/主号齐全）</summary>
    /// <param name="workOrderIds">工单ID列表</param>
    /// <param name="isPreInput">设置执行标记（null=不修改）</param>
    /// <param name="isMainNoMaterialComplete">设置主号齐全标记（null=不修改）</param>
    Task<int> SetPreExecuteFlagsAsync(List<int> workOrderIds, bool? isPreInput, bool? isMainNoMaterialComplete);
}
