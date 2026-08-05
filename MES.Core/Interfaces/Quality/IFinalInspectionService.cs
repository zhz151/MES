using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Quality;
namespace MES.Core.Interfaces.Quality;

/// <summary>
/// 成品检验服务接�?/// </summary>
public interface IFinalInspectionService
{
    /// <summary>
    /// 分页查询所有成品检验记�?    /// </summary>
    Task<PagedResult<FinalInspectionDto>> GetAllAsync(QueryParams query);

    /// <summary>
    /// 获取所有成品检验记录（无分页）
    /// </summary>
    Task<List<FinalInspectionDto>> GetAllListAsync();

    /// <summary>
    /// 获取成品检验详�?    /// </summary>
    Task<FinalInspectionDto?> GetByIdAsync(int id);

    /// <summary>
    /// 创建成品检验记�?    /// </summary>
    Task<FinalInspectionDto> CreateAsync(CreateFinalInspectionRequest request);

    /// <summary>
    /// 更新成品检验记�?    /// </summary>
    Task<FinalInspectionDto> UpdateAsync(int id, UpdateFinalInspectionRequest request);

    /// <summary>
    /// 删除成品检验记�?    /// </summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// 批量创建成品检验记�?    /// </summary>
    Task<List<FinalInspectionDto>> BatchCreateAsync(List<CreateFinalInspectionRequest> requests);

    /// <summary>
    /// 根据生产编号调取批次信息（用于新建页自动填充�?    /// </summary>
    Task<BatchLookupResultDto?> LookupBatchAsync(string batchNo);

    /// <summary>
    /// 获取筛选上下文（各列的 DISTINCT 值，用于 ExcelFilter�?    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>批量打印选中记录</summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>按条件打印全部记�?/summary>
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null, string? filters = null);

    /// <summary>
    /// 实时健康汇总（按当前筛选条件统计成检类型与成检到料不符的生产编号）
    /// </summary>
    Task<FinalInspectionHealthSummaryDto> GetFinalInspectionHealthSummaryAsync(QueryParams query);
}
