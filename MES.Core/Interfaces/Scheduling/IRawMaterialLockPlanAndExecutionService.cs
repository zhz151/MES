using MES.Core.Models;

using MES.Core.DTOs.WorkOrder;
using MES.Core.DTOs.Scheduling;
namespace MES.Core.Interfaces.Scheduling;

/// <summary>
/// 原锁计划服务接口（LEFT JOIN 实时查询�?/// </summary>
public interface IRawMaterialLockPlanAndExecutionService
{
    /// <summary>分页查询</summary>
    Task<PagedResult<RawMaterialLockPlanAndExecutionDto>> GetPagedAsync(QueryParams query);

    /// <summary>批量设置预执行标记（执行/预算投料�?主号齐全/预算主号齐全�?/summary>
    /// <param name="workOrderIds">工单ID列表</param>
    /// <param name="isPreInput">设置执行标记（null=不修改）</param>
    /// <param name="isMainNoMaterialComplete">设置主号齐全标记（null=不修改）</param>
    /// <param name="budgetInputDate">设置预算投料日（null=不修改）</param>
    /// <param name="isBudgetComplete">设置预算主号齐全（null=不修改）</param>
    Task<SetPreExecuteFlagsResult> SetPreExecuteFlagsAsync(List<int> workOrderIds, bool? isPreInput, bool? isMainNoMaterialComplete, DateTime? budgetInputDate = null, bool? isBudgetComplete = null);

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<MES.Core.DTOs.Shared.PrintColumnDef> columns);
}
