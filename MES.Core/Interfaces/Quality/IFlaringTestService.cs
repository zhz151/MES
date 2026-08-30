using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Quality;
namespace MES.Core.Interfaces.Quality;

public interface IFlaringTestService
{
    Task<PagedResult<FlaringTestDto>> GetAllAsync(QueryParams query);
    Task<FlaringTestDto?> GetByIdAsync(int id);
    Task<FlaringTestDto> CreateAsync(CreateFlaringTestRequest request);
    Task<FlaringTestDto> UpdateAsync(int id, UpdateFlaringTestRequest request);
    Task DeleteAsync(int id);
    Task<List<FlaringTestDto>> BatchCreateAsync(List<CreateFlaringTestRequest> requests);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
}
