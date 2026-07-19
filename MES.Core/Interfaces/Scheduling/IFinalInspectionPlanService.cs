
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

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<MES.Core.DTOs.Shared.PrintColumnDef> columns);
}
