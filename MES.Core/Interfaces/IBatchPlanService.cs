using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 在产明细计划服务接口 — 三表 LEFT JOIN 实时查询
/// </summary>
public interface IBatchPlanService
{
    /// <summary>
    /// 分页查询在产+未产批次计划
    /// </summary>
    Task<PagedResult<BatchPlanDto>> GetPagedAsync(QueryParams query);

    /// <summary>
    /// 获取列筛选上下文
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
