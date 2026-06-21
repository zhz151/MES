using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IGradePhysicalPropertyService
{
    Task<PagedResult<GradePhysicalPropertyDto>> GetPagedAsync(QueryParams query);
    Task<List<GradePhysicalPropertyDto>> GetAllAsync();
    Task<GradePhysicalPropertyDto> GetByIdAsync(int id);
    Task<GradePhysicalPropertyDto> CreateAsync(CreateGradePhysicalPropertyRequest request);
    Task<GradePhysicalPropertyDto> UpdateAsync(int id, UpdateGradePhysicalPropertyRequest request);
    Task DeleteAsync(int id);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
