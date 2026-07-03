using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 重点工序日产能力服务接口
/// </summary>
public interface IDailyProductionCapacityService
{
    /// <summary>分页查询</summary>
    Task<PagedResult<DailyProductionCapacityDto>> GetPagedAsync(QueryParams query);

    /// <summary>获取所有记录（用于下拉/缓存加载）</summary>
    Task<List<DailyProductionCapacityDto>> GetAllAsync();

    /// <summary>新增或更新</summary>
    Task<bool> SaveAsync(DailyProductionCapacityDto dto);

    /// <summary>删除</summary>
    Task<bool> DeleteAsync(int id);
}
