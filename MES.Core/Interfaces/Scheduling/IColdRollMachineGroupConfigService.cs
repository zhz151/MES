using MES.Core.DTOs.Scheduling;
using MES.Core.Models;

namespace MES.Core.Interfaces.Scheduling;

/// <summary>
/// 冷轧机台组配置服务 —— 冷轧工序归组参数表查询与维护（排程建议/排机估算引擎机台类型组归并输入）。
/// 供需链由 SupplyTargetGroupKey 显式表达（方案 A）：供给方组必须配置存在且非 None 的供给目标组、供给链无环，破坏抛 BusinessException。
/// </summary>
public interface IColdRollMachineGroupConfigService
{
    /// <summary>获取全部机台组配置（按 DisplayOrder 升序）</summary>
    Task<List<ColdRollMachineGroupConfigDto>> GetAllAsync();

    /// <summary>分页查询机台组配置（模糊搜索 + 排序）</summary>
    Task<PagedResult<ColdRollMachineGroupConfigDto>> GetPagedAsync(QueryParams query);

    /// <summary>保存机台组配置（新增/更新）</summary>
    Task<bool> SaveAsync(ColdRollMachineGroupConfigDto dto);

    /// <summary>删除机台组配置</summary>
    Task<bool> DeleteAsync(int id);
}
