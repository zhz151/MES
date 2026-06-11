using MES.Core.DTOs;

namespace MES.Core.Interfaces;

/// <summary>
/// 冷轧计划看板服务 — 按规格维度聚合生产批次的时间桶重量分布
/// </summary>
public interface IColdRollPlanService
{
    /// <summary>
    /// 获取冷轧计划看板数据
    /// </summary>
    /// <param name="sectionFilter">工段筛选：null=全部, "60冷轧", "50冷轧", "30冷轧", "20冷轧", "三辊冷轧", "冷拔"</param>
    Task<List<ColdRollPlanRowDto>> GetPlanAsync(string? sectionFilter);

}
