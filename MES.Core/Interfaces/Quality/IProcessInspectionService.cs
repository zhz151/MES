using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Quality;
namespace MES.Core.Interfaces.Quality;

/// <summary>
/// 过程检验服务接�?/// </summary>
public interface IProcessInspectionService
{
    /// <summary>
    /// 跨批次查询所有过程检验记录（分页�?    /// </summary>
    Task<PagedResult<ProcessInspectionDto>> GetAllAsync(QueryParams query);

    /// <summary>
    /// 获取所有过程检验记录（无分页）
    /// </summary>
    Task<List<ProcessInspectionDto>> GetAllListAsync();

    /// <summary>
    /// 获取筛选上下文（各列去重值），用�?ExcelFilter 下拉选项
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>批量打印选中记录</summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>按条件打印全部记�?/summary>
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null);

    /// <summary>
    /// 批量创建过程检验记�?    /// </summary>
    Task<List<ProcessInspectionDto>> BatchCreateAsync(List<CreateProcessInspectionRequest> requests);

    /// <summary>
    /// 更新过程检验记�?    /// </summary>
    Task<ProcessInspectionDto> UpdateAsync(int id, UpdateProcessInspectionRequest request);

    /// <summary>
    /// 删除过程检验记�?    /// </summary>
    Task DeleteAsync(int id);
}
