using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Quality;
namespace MES.Core.Interfaces.Quality;

public interface IHardnessTestService
{
    Task<PagedResult<HardnessTestDto>> GetAllAsync(QueryParams query);
    Task<HardnessTestDto?> GetByIdAsync(int id);
    Task<HardnessTestDto> CreateAsync(CreateHardnessTestRequest request);
    Task<HardnessTestDto> UpdateAsync(int id, UpdateHardnessTestRequest request);
    Task DeleteAsync(int id);
    Task<List<HardnessTestDto>> BatchCreateAsync(List<CreateHardnessTestRequest> requests);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null);
}
