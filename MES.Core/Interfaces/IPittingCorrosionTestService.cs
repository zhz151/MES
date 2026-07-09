using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IPittingCorrosionTestService
{
    Task<PagedResult<PittingCorrosionTestDto>> GetAllAsync(QueryParams query);
    Task<PittingCorrosionTestDto?> GetByIdAsync(int id);
    Task<PittingCorrosionTestDto> CreateAsync(CreatePittingCorrosionTestRequest request);
    Task<PittingCorrosionTestDto> UpdateAsync(int id, UpdatePittingCorrosionTestRequest request);
    Task DeleteAsync(int id);
    Task<List<PittingCorrosionTestDto>> BatchCreateAsync(List<CreatePittingCorrosionTestRequest> requests);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null);
}
