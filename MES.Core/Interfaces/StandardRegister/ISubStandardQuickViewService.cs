using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.ProductionStandard;
namespace MES.Core.Interfaces.ProductionStandard;

public interface ISubStandardQuickViewService
{
    Task<PagedResult<SubStandardQuickViewDto>> GetPagedAsync(QueryParams query);
    Task<SubStandardQuickViewDto> GetByIdAsync(int id);
    Task<SubStandardQuickViewDto> CreateAsync(CreateSubStandardQuickViewRequest request);
    Task<SubStandardQuickViewDto> UpdateAsync(int id, UpdateSubStandardQuickViewRequest request);
    Task DeleteAsync(int id);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>批量打印选中记录</summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>按条件打印全部记�?/summary>
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns);
}
