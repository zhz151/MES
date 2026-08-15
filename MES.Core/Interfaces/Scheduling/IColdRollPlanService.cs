
using MES.Core.DTOs.Scheduling;
namespace MES.Core.Interfaces.Scheduling;

/// <summary>
/// 冷轧计划看板服务 �?按规格维度聚合生产批次的时间桶重量分�?/// </summary>
public interface IColdRollPlanService
{
    /// <summary>
    /// 获取冷轧计划看板数据
    /// </summary>
    /// <param name="sectionFilter">工段筛选：null=全部, "60冷轧", "50冷轧", "30冷轧", "20冷轧", "三辊冷轧", "冷拔"</param>
    Task<List<ColdRollPlanRowDto>> GetPlanAsync(string? sectionFilter);

    /// <summary>
    /// 获取冷轧排程汇总（分档与主列表统一：在轧/待轧 各分 总量/特急/急/余量）
    /// </summary>
    /// <param name="sectionFilter">工段筛选：null=全部</param>
    /// <param name="maxDiff">工量差筛选：null=全部(待轧近), 2=近2天, 4=近4天</param>
    Task<List<ColdRollPlanSummaryDto>> GetScheduleSummaryAsync(string? sectionFilter, int? maxDiff = null);

}
