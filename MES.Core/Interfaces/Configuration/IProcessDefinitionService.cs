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

    /// <summary>获取冷轧或冷拔 Key 集合（IsColdRoll || IsColdDraw），IMemoryCache 5 分钟</summary>
    Task<HashSet<string>> GetColdRollOrDrawKeysAsync();

    /// <summary>
    /// 获取冷轧或冷拔工序选项（IsColdRoll || IsColdDraw，**且 IsEnabled=true 仅启用工序**，按 DisplayOrder 升序），
    /// 机型下拉/工段 Tab/机台组配置工序多选动态化用；禁用工序不参与归组/机台数配置/工段 Tab。
    /// </summary>
    Task<List<ProcessInfoDto>> GetColdRollOrDrawOptionsAsync();
}
