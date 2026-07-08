using MES.Core.DTOs;

namespace MES.Core.Interfaces;

/// <summary>
/// 工段流转分类设置服务接口（参数表管理）
/// </summary>
public interface ISectionFlowCategoryService
{
    /// <summary>获取所有设置含明细</summary>
    Task<List<SectionFlowCategorySettingDto>> GetSettingsAsync();

    /// <summary>更新类别字段</summary>
    Task<bool> SaveSettingAsync(SectionFlowCategorySettingDto dto);

    /// <summary>更新类别生产目标</summary>
    Task<bool> UpdateSettingAsync(SectionFlowSettingUpdateDto dto);

    /// <summary>更新明细系数</summary>
    Task<bool> SaveItemAsync(int itemId, SectionFlowCategoryItemDto dto);

    /// <summary>删除明细</summary>
    Task<bool> DeleteItemAsync(int itemId);

    /// <summary>新增明细</summary>
    Task<bool> CreateItemAsync(int settingId, SectionFlowCategoryItemDto dto);
}
