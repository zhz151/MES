using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 质量过程跟踪服务接口
/// </summary>
public interface IQualityProcessTrackingService
{
    /// <summary>分页查询质量过程跟踪数据</summary>
    Task<PagedResult<QualityProcessTrackingDto>> GetPagedAsync(QueryParams query);

    /// <summary>获取筛选上下文（各列去重值）</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
