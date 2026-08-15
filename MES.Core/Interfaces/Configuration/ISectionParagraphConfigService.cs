
using MES.Core.DTOs.Configuration;
namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 段落日产配置服务接口（参数表管理）
/// </summary>
public interface ISectionParagraphConfigService
{
    /// <summary>获取所有段落设置</summary>
    Task<List<SectionParagraphConfigDto>> GetSettingsAsync();

    /// <summary>新增段落</summary>
    Task<bool> CreateSettingAsync(SectionParagraphConfigDto dto);

    /// <summary>删除段落（组合归类表「归属段落」置空）</summary>
    Task<bool> DeleteSettingAsync(int id);

    /// <summary>更新段落字段</summary>
    Task<bool> SaveSettingAsync(SectionParagraphConfigDto dto);
}
