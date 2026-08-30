using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Quality;
namespace MES.Core.Interfaces.Quality;

public interface IMetallographicTestService
{
    Task<PagedResult<MetallographicTestDto>> GetAllAsync(QueryParams query);
    Task<MetallographicTestDto?> GetByIdAsync(int id);
    Task<MetallographicTestDto> CreateAsync(CreateMetallographicTestRequest request);
    Task<MetallographicTestDto> UpdateAsync(int id, UpdateMetallographicTestRequest request);
    Task DeleteAsync(int id);
    Task<List<MetallographicTestDto>> BatchCreateAsync(List<CreateMetallographicTestRequest> requests);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
}
