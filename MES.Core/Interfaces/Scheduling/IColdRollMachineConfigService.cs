using MES.Core.DTOs.Scheduling;
using MES.Core.Models;

namespace MES.Core.Interfaces.Scheduling;

/// <summary>
/// 冷轧机台数配置服务 —— 机台数参数表查询与维护（纯手工参数，排程建议引擎产能平衡输入）
/// </summary>
public interface IColdRollMachineConfigService
{
    /// <summary>获取全部机台数配置（按机型升序）</summary>
    Task<List<ColdRollMachineConfigDto>> GetAllAsync();

    /// <summary>分页查询机台数配置（模糊搜索 + 排序）</summary>
    Task<PagedResult<ColdRollMachineConfigDto>> GetPagedAsync(QueryParams query);

    /// <summary>保存机台数配置（新增/更新）</summary>
    Task<bool> SaveAsync(ColdRollMachineConfigDto dto);

    /// <summary>删除机台数配置</summary>
    Task<bool> DeleteAsync(int id);
}
