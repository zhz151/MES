// 文件路径: MES.Core/Interfaces/IStandardProcessCycleService.cs
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 标准工艺生产周期服务接口
/// </summary>
public interface IStandardProcessCycleService
{
    /// <summary>分页查询（支持关键字搜索 + 筛选）</summary>
    Task<PagedResult<StandardProcessCycleDto>> GetPagedAsync(QueryParams query);

    /// <summary>获取全部</summary>
    Task<List<StandardProcessCycleDto>> GetAllAsync();

    /// <summary>根据ID获取</summary>
    Task<StandardProcessCycleDto?> GetByIdAsync(int id);

    /// <summary>创建</summary>
    Task<StandardProcessCycleDto> CreateAsync(CreateStandardProcessCycleRequest request);

    /// <summary>更新</summary>
    Task<StandardProcessCycleDto> UpdateAsync(int id, UpdateStandardProcessCycleRequest request);

    /// <summary>删除</summary>
    Task DeleteAsync(int id);

    /// <summary>获取筛选上下文（各列去重值）</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
