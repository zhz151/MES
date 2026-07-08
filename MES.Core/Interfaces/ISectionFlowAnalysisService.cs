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
}
