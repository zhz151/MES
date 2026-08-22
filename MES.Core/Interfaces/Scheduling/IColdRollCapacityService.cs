using MES.Core.DTOs.Scheduling;
using MES.Core.Models;

namespace MES.Core.Interfaces.Scheduling;

/// <summary>
/// 冷轧产能配置服务 —— 产能档案查询与手工调整（排程保存反哺由 ColdRollSpecScheduleService 内联完成）
/// </summary>
public interface IColdRollCapacityService
{
    /// <summary>获取全部产能配置（按四维升序）</summary>
    Task<List<ColdRollCapacityDto>> GetAllAsync();

    /// <summary>分页查询产能配置（模糊搜索 + 排序）</summary>
    Task<PagedResult<ColdRollCapacityDto>> GetPagedAsync(QueryParams query);

    /// <summary>保存产能配置（更新机台/日产能，SampleCount++，并反向同步排程小表已存在维度）</summary>
    Task<bool> SaveAsync(ColdRollCapacityDto dto);
}
