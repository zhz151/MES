using MES.Core.DTOs;

namespace MES.Core.Interfaces;

/// <summary>
/// 生产段流转量分析服务接口
/// </summary>
public interface ISectionFlowAnalysisService
{
    /// <summary>获取全部分析数据（含计算字段）</summary>
    Task<List<SectionFlowAnalysisDto>> GetAnalysisAsync();

    /// <summary>更新段落分类设置</summary>
    Task<bool> UpdateSettingAsync(SectionFlowSettingUpdateDto dto);

    // ========== 参数表管理 ==========

    /// <summary>获取所有设置含明细（不做计算，返回原始数据）</summary>
    Task<List<SectionFlowCategorySettingDto>> GetSettingsAsync();

    /// <summary>更新类别字段</summary>
    Task<bool> SaveSettingAsync(SectionFlowCategorySettingDto dto);

    /// <summary>更新明细系数</summary>
    Task<bool> SaveItemAsync(int itemId, SectionFlowCategoryItemDto dto);

    /// <summary>删除明细</summary>
    Task<bool> DeleteItemAsync(int itemId);

    /// <summary>新增明细</summary>
    Task<bool> CreateItemAsync(int settingId, SectionFlowCategoryItemDto dto);
}
