using MES.Core.DTOs.Configuration;

namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 工艺卡打印版式配置服务接口：管理打印字体/字号（Key→Value 键值对），
/// 数据库全局共享（仿 ProcessCardColumnDefinition 模式）。
/// </summary>
public interface IProcessCardStyleDefinitionService
{
    /// <summary>全量配置（格式设置面板「打印版式」Tab 加载），按 Key 升序</summary>
    Task<List<ProcessCardStyleDefinitionDto>> GetAllAsync();

    /// <summary>配置映射：Key → Value（打印链路覆盖字体/字号用），IMemoryCache 5 分钟</summary>
    Task<Dictionary<string, string>> GetStyleMapAsync();

    /// <summary>批量新增/更新（锚点 Key），校验后写入并清缓存，返回写入行数</summary>
    Task<int> SaveAllAsync(List<ProcessCardStyleDefinitionDto> items);
}
