using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

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
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null);
}
