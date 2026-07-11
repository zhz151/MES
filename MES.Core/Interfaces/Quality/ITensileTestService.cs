using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Quality;
namespace MES.Core.Interfaces.Quality;

public interface ITensileTestService
{
    Task<PagedResult<TensileTestDto>> GetAllAsync(QueryParams query);
    Task<TensileTestDto?> GetByIdAsync(int id);
    Task<TensileTestDto> CreateAsync(CreateTensileTestRequest request);
    Task<TensileTestDto> UpdateAsync(int id, UpdateTensileTestRequest request);
    Task DeleteAsync(int id);
    Task<List<TensileTestDto>> BatchCreateAsync(List<CreateTensileTestRequest> requests);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null);
}
