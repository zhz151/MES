using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.StandardRegister;
namespace MES.Core.Interfaces.StandardRegister;

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
}
