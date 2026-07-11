using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.ProductionStandard;
namespace MES.Core.Interfaces.ProductionStandard;

public interface IGradePhysicalPropertyService
{
    Task<PagedResult<GradePhysicalPropertyDto>> GetPagedAsync(QueryParams query);
    Task<List<GradePhysicalPropertyDto>> GetAllAsync();
    Task<GradePhysicalPropertyDto> GetByIdAsync(int id);
    Task<GradePhysicalPropertyDto> CreateAsync(CreateGradePhysicalPropertyRequest request);
    Task<GradePhysicalPropertyDto> UpdateAsync(int id, UpdateGradePhysicalPropertyRequest request);
    Task DeleteAsync(int id);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns);
}
