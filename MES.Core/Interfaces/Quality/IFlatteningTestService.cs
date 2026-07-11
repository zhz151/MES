using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Quality;
namespace MES.Core.Interfaces.Quality;

public interface IFlatteningTestService
{
    Task<PagedResult<FlatteningTestDto>> GetAllAsync(QueryParams query);
    Task<FlatteningTestDto?> GetByIdAsync(int id);
    Task<FlatteningTestDto> CreateAsync(CreateFlatteningTestRequest request);
    Task<FlatteningTestDto> UpdateAsync(int id, UpdateFlatteningTestRequest request);
    Task DeleteAsync(int id);
    Task<List<FlatteningTestDto>> BatchCreateAsync(List<CreateFlatteningTestRequest> requests);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null);
}
