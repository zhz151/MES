
using MES.Core.DTOs.Configuration;
namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 段落日产配置服务接口（参数表管理）。段落由 3 类配置自动生成，仅参数可编辑。
/// </summary>
public interface ISectionParagraphConfigService
{
    /// <summary>获取所有段落设置（内部自动同步 3 类配置展开的期望段落集）</summary>
    Task<List<SectionParagraphConfigDto>> GetSettingsAsync();

    /// <summary>更新段落参数（日流转设定/偏少天数/过多天数/备注）</summary>
    Task<bool> SaveSettingAsync(SectionParagraphConfigDto dto);
}
