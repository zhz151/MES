
using MES.Core.DTOs.Scheduling;
namespace MES.Core.Interfaces.Scheduling;

/// <summary>
/// 成检计划服务接口
/// </summary>
public interface IFinalInspectionPlanService
{
    /// <summary>
    /// 获取成检计划数据，按三档分组
    /// </summary>
    Task<List<FinalInspectionPlanDto>> GetKanbanAsync();
}
