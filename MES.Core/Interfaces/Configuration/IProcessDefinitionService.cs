using MES.Core.Models;

using MES.Core.DTOs.Configuration;

namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 工序组定义服务接口：配置表管理 + 显示映射 + 冷轧类判定。
/// </summary>
public interface IProcessDefinitionService
{
    /// <summary>分页查询</summary>
    Task<PagedResult<ProcessDefinitionDto>> GetPagedAsync(QueryParams query);

    /// <summary>根据 ID 获取</summary>
    Task<ProcessDefinitionDto?> GetByIdAsync(int id);

    /// <summary>新增或更新</summary>
    Task<bool> SaveAsync(ProcessDefinitionDto dto);

    /// <summary>删除</summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// 获取启用工序列表（IsEnabled=true 且按 DisplayOrder 升序），下拉/展示层动态化用。
    /// </summary>
    Task<List<ProcessInfoDto>> GetEnabledProcessesAsync();

    /// <summary>获取 Key → 显示中文 映射（配置表优先，兜底 ProcessKeys），IMemoryCache 5 分钟</summary>
    Task<IReadOnlyDictionary<string, string>> GetProcessNameMapAsync();

    /// <summary>归一为显示中文：Key → 中文；已是中文原样返回；未知返回 null</summary>
    Task<string?> ToDisplayAsync(string? keyOrName);

    /// <summary>归一为稳定 Key：已是 Key 原样返回；中文反查；未知返回 null</summary>
    Task<string?> ToKeyAsync(string? nameOrKey);

    /// <summary>获取冷轧系列 Key 集合（IsColdRoll=true），IMemoryCache 5 分钟</summary>
    Task<HashSet<string>> GetColdRollKeysAsync();

    /// <summary>获取冷轧或冷拔 Key 集合（IsColdRoll || IsColdDraw），IMemoryCache 5 分钟</summary>
    Task<HashSet<string>> GetColdRollOrDrawKeysAsync();
}
