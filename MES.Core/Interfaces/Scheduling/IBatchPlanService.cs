using MES.Core.Models;

using MES.Core.DTOs.Scheduling;
namespace MES.Core.Interfaces.Scheduling;

/// <summary>
/// 在产明细计划服务接口 �?三表 LEFT JOIN 实时查询
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

    /// <summary>
    /// 全量加载（含冷轧排程维度），按工段筛选后返回全部记录
    /// </summary>
    Task<List<BatchPlanDto>> GetAllAsync(string? sectionTab);

    /// <summary>
    /// 获取冷轧排程流转汇�?�?基于批次看板实际 IsFlow 判定结果，按(FlowCRType, 外径跨度)聚合
    /// </summary>
    /// <param name="sectionTab">工段筛�?/param>
    /// <param name="maxDiff">最大原工量差筛选：null=全部，n=原工量差小于等于n的流转批�?/param>
    Task<List<ColdRollScheduleSummaryDto>> GetFlowSummaryAsync(string? sectionTab, int? maxDiff = null);
}
