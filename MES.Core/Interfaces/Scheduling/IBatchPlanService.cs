using MES.Core.Models;

using MES.Core.DTOs.Scheduling;
namespace MES.Core.Interfaces.Scheduling;

/// <summary>
/// 在产明细计划服务接口 —— 三表 LEFT JOIN 实时查询
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
    /// 跨工段汇总（实时查询）：按工段 Tab 逐工段归桶统计批次数/总重量/流转/重点/等级分布，末尾追加"合计"行（全量唯一批次）。
    /// 每工段行口径与 GetAllAsync(sectionTab) 完全一致；一个批次可能命中多个工段，故各工段行批次数之和可能大于合计。
    /// </summary>
    Task<List<BatchPlanSummaryRowDto>> GetSummaryAsync();

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<MES.Core.DTOs.Shared.PrintColumnDef> columns);
}
