
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Configuration;
namespace MES.Core.Interfaces.Scheduling;

/// <summary>
/// 生产段流转量分析服务接口
/// </summary>
public interface ISectionFlowAnalysisService
{
    /// <summary>获取全部分析数据（含计算字段�?/summary>
    Task<List<SectionFlowAnalysisDto>> GetAnalysisAsync();

    /// <summary>更新段落分类设置</summary>
    Task<bool> UpdateSettingAsync(SectionFlowSettingUpdateDto dto);
}
