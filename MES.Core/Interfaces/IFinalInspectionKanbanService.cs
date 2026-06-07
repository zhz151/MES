using MES.Core.DTOs;

namespace MES.Core.Interfaces;

/// <summary>
/// 成检看板服务接口
/// </summary>
public interface IFinalInspectionKanbanService
{
    /// <summary>
    /// 获取成检看板数据，按三档分组
    /// </summary>
    Task<List<FinalInspectionKanbanDto>> GetKanbanAsync();
}
