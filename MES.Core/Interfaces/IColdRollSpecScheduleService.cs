using MES.Core.DTOs;

namespace MES.Core.Interfaces;

/// <summary>
/// 冷轧排程服务 — ColdRollSpecSchedule 的全量读写
/// </summary>
public interface IColdRollSpecScheduleService
{
    /// <summary>获取所有排程记录</summary>
    Task<List<ColdRollSpecScheduleDto>> GetAllAsync();

    /// <summary>全量保存排程记录（增/改 + 删除不在列表中的旧记录）</summary>
    Task SaveAllAsync(List<ColdRollSpecScheduleDto> dtos);
}
