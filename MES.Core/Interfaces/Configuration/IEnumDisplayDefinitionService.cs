using MES.Core.Models;

using MES.Core.DTOs.Configuration;

namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 枚举显示配置服务接口：管理 C# 强类型枚举的中文显示名与排序（不改值域）。
/// </summary>
public interface IEnumDisplayDefinitionService
{
    /// <summary>分页查询</summary>
    Task<PagedResult<EnumDisplayDefinitionDto>> GetPagedAsync(QueryParams query);

    /// <summary>根据 ID 获取</summary>
    Task<EnumDisplayDefinitionDto?> GetByIdAsync(int id);

    /// <summary>新增或更新</summary>
    Task<bool> SaveAsync(EnumDisplayDefinitionDto dto);

    /// <summary>删除</summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// 全量显示映射：EnumKey → Value → DisplayName（配置表优先，兜底 EnumHelper 静态字典）。
    /// 前端显示层/筛选下拉/DataExchange 用，IMemoryCache 5 分钟。
    /// </summary>
    Task<Dictionary<string, Dictionary<string, string>>> GetDisplayMapAsync();

    /// <summary>
    /// 全量显示选项：EnumKey → (Value/DisplayName/DisplayOrder) 有序列表（按 DisplayOrder 升序）。
    /// 前端 MainLayout 注入 EnumHelper.ApplyEnumOrder，使列筛选/表单下拉按配置排序。
    /// </summary>
    Task<Dictionary<string, List<EnumDisplayOptionDto>>> GetOptionsMapAsync();

    /// <summary>
    /// 恢复默认：为该 EnumKey 生成静态兜底（EnumHelper）中缺失的默认行。
    /// 已存在（含用户改过的中文）不覆盖，返回新增行数。
    /// </summary>
    Task<int> RestoreDefaultsAsync(string enumKey);

    /// <summary>列筛选上下文：可筛列的 DISTINCT 值（EnumKey/Value/DisplayName/Remark），供前端 ExcelFilter 下拉加载</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
