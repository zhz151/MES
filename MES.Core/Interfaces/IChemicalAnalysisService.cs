using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 化学分析服务接口
/// </summary>
public interface IChemicalAnalysisService
{
    /// <summary>分页查询所有化学分析记录</summary>
    Task<PagedResult<ChemicalAnalysisDto>> GetAllAsync(QueryParams query);

    /// <summary>获取化学分析详情</summary>
    Task<ChemicalAnalysisDto?> GetByIdAsync(int id);

    /// <summary>创建化学分析记录</summary>
    Task<ChemicalAnalysisDto> CreateAsync(CreateChemicalAnalysisRequest request);

    /// <summary>更新化学分析记录</summary>
    Task<ChemicalAnalysisDto> UpdateAsync(int id, UpdateChemicalAnalysisRequest request);

    /// <summary>删除化学分析记录</summary>
    Task DeleteAsync(int id);

    /// <summary>批量创建化学分析记录</summary>
    Task<List<ChemicalAnalysisDto>> BatchCreateAsync(List<CreateChemicalAnalysisRequest> requests);

    /// <summary>获取筛选上下文（各列的 DISTINCT 值）</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
