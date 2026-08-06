using MES.Core.Models;

using MES.Core.DTOs.Configuration;

namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 字典值配置服务接口：管理 string 存储字典字段（工段/工序/紧急度/产类/流转/关注目标/汇总行/责任类别）
/// 的中文显示名、排序、隐藏与可加值。
/// </summary>
public interface IDictValueDefinitionService
{
    /// <summary>分页查询</summary>
    Task<PagedResult<DictValueDefinitionDto>> GetPagedAsync(QueryParams query);

    /// <summary>根据 ID 获取</summary>
    Task<DictValueDefinitionDto?> GetByIdAsync(int id);

    /// <summary>新增或更新</summary>
    Task<bool> SaveAsync(DictValueDefinitionDto dto);

    /// <summary>删除</summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// 全量显示映射：DictKey → Value → DisplayName（配置表优先，兜底各 Keys 常量类）。
    /// 前端显示层/筛选下拉/DataExchange 用，IMemoryCache 5 分钟。
    /// </summary>
    Task<Dictionary<string, Dictionary<string, string>>> GetDisplayMapAsync();

    /// <summary>
    /// 启用字典值列表：配置表 IsEnabled=true 按 DisplayOrder 升序；配置表中不存在（含被隐藏）的静态值追加末尾。
    /// 供下拉选项动态加载（如责任类型下拉），隐藏/加值即时生效。
    /// </summary>
    Task<List<DictValueInfoDto>> GetEnabledValuesAsync(string dictKey);

    /// <summary>
    /// 恢复默认：为该 DictKey 生成静态兜底（各 Keys 常量类）中缺失的默认行。
    /// 已存在（含用户改过的中文）不覆盖，返回新增行数。
    /// </summary>
    Task<int> RestoreDefaultsAsync(string dictKey);
}
