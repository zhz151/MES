using MES.Core.DTOs.Configuration;

namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 工艺卡打印列布局配置服务接口：管理每个打印字段的显示配置（启用/所属行/列顺序/列权重），
/// 数据库全局共享（仿 EnumDisplayDefinition 模式）。
/// </summary>
public interface IProcessCardColumnDefinitionService
{
    /// <summary>全量配置（格式设置面板加载），按 BlockKey 升序 + ColumnIndex 升序</summary>
    Task<List<ProcessCardColumnDefinitionDto>> GetAllAsync();

    /// <summary>
    /// 配置映射：$"{BlockKey}|{FieldKey}" → 配置 DTO（打印覆盖请求列定义用），IMemoryCache 5 分钟。
    /// </summary>
    Task<Dictionary<string, ProcessCardColumnDefinitionDto>> GetConfigMapAsync();

    /// <summary>
    /// 批量新增/更新（锚点 BlockKey+FieldKey），校验后写入并清缓存，返回写入行数。
    /// </summary>
    Task<int> SaveAllAsync(List<ProcessCardColumnDefinitionDto> items);
}
