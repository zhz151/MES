using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface ISubStandardQuickViewService
{
    Task<PagedResult<SubStandardQuickViewDto>> GetPagedAsync(QueryParams query);
    Task<SubStandardQuickViewDto> GetByIdAsync(int id);
    Task<SubStandardQuickViewDto> CreateAsync(CreateSubStandardQuickViewRequest request);
    Task<SubStandardQuickViewDto> UpdateAsync(int id, UpdateSubStandardQuickViewRequest request);
    Task DeleteAsync(int id);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
