using MES.Core.DTOs.Configuration;

namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 组合归类服务接口 — 以(工序组, 工段, 产类)为基准的归属映射 CRUD
/// </summary>
public interface ICombinationGroupService
{
    /// <summary>获取全部组合归类（含流转类别名称）</summary>
    Task<List<CombinationGroupDto>> GetListAsync();

    /// <summary>新增或更新组合归类</summary>
    Task<bool> SaveAsync(CombinationGroupDto dto);

    /// <summary>删除组合归类</summary>
    Task<bool> DeleteAsync(int id);
}
