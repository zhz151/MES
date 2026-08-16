
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

    /// <summary>
    /// 获取成检计划「待检批支重汇总」（行=检验项，列=待到料/待检验/检验中/汇总数据；
    /// 统计要求该检验项且尚未完成该检验的看板批次，每列批次数/生产支数/生产重量）
    /// </summary>
    Task<List<FinalInspectionPlanSummaryRowDto>> GetSummaryAsync();

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<MES.Core.DTOs.Shared.PrintColumnDef> columns);
}
