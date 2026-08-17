using MES.Core.DTOs.Configuration;

namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 质量证明书打印配置服务接口：管理页眉企业信息/标题/页脚说明/字体字号（Key→Value 键值对），
/// 数据库全局共享（仿 ProcessCardStyleDefinition 模式）。
/// </summary>
public interface ICertificatePrintSettingService
{
    /// <summary>全量配置（「打印设置」对话框加载），按 Key 升序</summary>
    Task<List<CertificatePrintSettingDto>> GetAllAsync();

    /// <summary>配置映射：Key → Value（打印链路覆盖企业信息/页脚/字体用），IMemoryCache 5 分钟</summary>
    Task<Dictionary<string, string>> GetSettingMapAsync();

    /// <summary>批量新增/更新（锚点 Key），校验后写入并清缓存，返回写入行数</summary>
    Task<int> SaveAllAsync(List<CertificatePrintSettingDto> items);
}
