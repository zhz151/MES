using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Quality;
namespace MES.Core.Interfaces.Quality;

public interface IGrainSizeTestService
{
    Task<PagedResult<GrainSizeTestDto>> GetAllAsync(QueryParams query);
    Task<GrainSizeTestDto?> GetByIdAsync(int id);
    Task<GrainSizeTestDto> CreateAsync(CreateGrainSizeTestRequest request);
    Task<GrainSizeTestDto> UpdateAsync(int id, UpdateGrainSizeTestRequest request);
    Task DeleteAsync(int id);
    Task<List<GrainSizeTestDto>> BatchCreateAsync(List<CreateGrainSizeTestRequest> requests);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null);
}
