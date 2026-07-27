using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Quality;
namespace MES.Core.Interfaces.Quality;

/// <summary>
/// 质量过程跟踪服务接口
/// </summary>
public interface IQualityProcessTrackingService
{
    /// <summary>分页查询质量过程跟踪数据</summary>
    Task<PagedResult<QualityProcessTrackingDto>> GetPagedAsync(QueryParams query);

    /// <summary>获取筛选上下文（各列去重值）</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>批量打印选中记录</summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>按条件打印全部记录</summary>
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? receiveDateFrom = null, DateTime? receiveDateTo = null, string? filters = null);

    /// <summary>按成检到料ID刷新物化�?/summary>
    Task RefreshByMrCheckIdAsync(int mrCheckId);

    /// <summary>按批次ID刷新物化�?/summary>
    Task RefreshByProductionBatchIdAsync(int productionBatchId);

    /// <summary>按批次号刷新物化�?/summary>
    Task RefreshByBatchNoAsync(string batchNo);
}
