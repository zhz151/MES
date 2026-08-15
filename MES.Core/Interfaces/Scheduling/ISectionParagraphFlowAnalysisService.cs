
using MES.Core.DTOs.Scheduling;
namespace MES.Core.Interfaces.Scheduling;

/// <summary>
/// 生产段落流转量分析服务接口
/// </summary>
public interface ISectionParagraphFlowAnalysisService
{
    /// <summary>获取全部分析数据（按生产段落分类汇总）</summary>
    Task<List<SectionParagraphFlowAnalysisDto>> GetAnalysisAsync();

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<MES.Core.DTOs.Shared.PrintColumnDef> columns);
}
