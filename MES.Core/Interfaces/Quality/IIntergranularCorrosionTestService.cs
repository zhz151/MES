using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Quality;
namespace MES.Core.Interfaces.Quality;

public interface IIntergranularCorrosionTestService
{
    Task<PagedResult<IntergranularCorrosionTestDto>> GetAllAsync(QueryParams query);
    Task<IntergranularCorrosionTestDto?> GetByIdAsync(int id);
    Task<IntergranularCorrosionTestDto> CreateAsync(CreateIntergranularCorrosionTestRequest request);
    Task<IntergranularCorrosionTestDto> UpdateAsync(int id, UpdateIntergranularCorrosionTestRequest request);
    Task DeleteAsync(int id);
    Task<List<IntergranularCorrosionTestDto>> BatchCreateAsync(List<CreateIntergranularCorrosionTestRequest> requests);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null);
}
