using MES.Core.Models;

using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Shared;
namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 工位管理服务接口
/// </summary>
public interface IWorkstationService
{
    /// <summary>分页查询</summary>
    Task<PagedResult<WorkstationDto>> GetPagedAsync(QueryParams query);

    /// <summary>按工位编码查�?/summary>
    Task<WorkstationDto?> GetByCodeAsync(string code);

    /// <summary>新增或更�?/summary>
    Task<bool> SaveAsync(WorkstationDto dto);

    /// <summary>删除</summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>批量打印（按ID）</summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>按条件打印全部</summary>
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns);
}
