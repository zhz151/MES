
using MES.Core.DTOs.Scheduling;
namespace MES.Core.Interfaces.Scheduling;

/// <summary>
/// 批次计划产量目标服务接口
/// </summary>
public interface IBatchPlanTargetService
{
    /// <summary>获取所有工段的产量目标</summary>
    Task<List<BatchPlanTargetDto>> GetAllAsync();

    /// <summary>批量保存产量目标（全量覆盖）</summary>
    Task<bool> SaveAllAsync(List<BatchPlanTargetDto> dtos);
}
