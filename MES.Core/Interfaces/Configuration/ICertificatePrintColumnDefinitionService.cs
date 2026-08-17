using MES.Core.DTOs.Configuration;

namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 质量证明书打印列布局配置服务接口：管理明细表（物料/化学/检验检测）每个打印字段的显示配置
/// （启用/列顺序/列宽权重），数据库全局共享（仿 ProcessCardColumnDefinition 模式）。
/// </summary>
public interface ICertificatePrintColumnDefinitionService
{
    /// <summary>全量配置（「字段布局」面板加载），按 BlockKey 升序 + ColumnIndex 升序</summary>
    Task<List<CertificatePrintColumnDefinitionDto>> GetAllAsync();

    /// <summary>
    /// 配置映射：$"{BlockKey}|{FieldKey}" → 配置 DTO（打印链路覆盖默认列定义用），IMemoryCache 5 分钟。
    /// </summary>
    Task<Dictionary<string, CertificatePrintColumnDefinitionDto>> GetConfigMapAsync();

    /// <summary>
    /// 批量新增/更新（锚点 BlockKey+FieldKey），校验后写入并清缓存，返回写入行数。
    /// </summary>
    Task<int> SaveAllAsync(List<CertificatePrintColumnDefinitionDto> items);
}
