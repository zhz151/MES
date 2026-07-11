
using MES.Core.DTOs.Scheduling;
namespace MES.Core.Interfaces.Scheduling;

/// <summary>
/// 冷轧排程服务 �?ColdRollSpecSchedule 的全量读�?/// </summary>
public interface IColdRollSpecScheduleService
{
    /// <summary>获取所有排程记�?/summary>
    Task<List<ColdRollSpecScheduleDto>> GetAllAsync();

    /// <summary>全量保存排程记录（增/�?+ 删除不在列表中的旧记录）</summary>
    Task SaveAllAsync(List<ColdRollSpecScheduleDto> dtos);
}
