
using MES.Core.DTOs.Configuration;
namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 工段流转分类设置服务接口（参数表管理）
/// </summary>
public interface ISectionFlowCategoryService
{
    /// <summary>获取所有类别设置</summary>
    Task<List<SectionFlowCategorySettingDto>> GetSettingsAsync();

    /// <summary>新增类别</summary>
    Task<bool> CreateSettingAsync(SectionFlowCategorySettingDto dto);

    /// <summary>删除类别（级联删组合归类行）</summary>
    Task<bool> DeleteSettingAsync(int id);

    /// <summary>更新类别字段</summary>
    Task<bool> SaveSettingAsync(SectionFlowCategorySettingDto dto);

    /// <summary>更新类别生产目标</summary>
    Task<bool> UpdateSettingAsync(SectionFlowSettingUpdateDto dto);
}
