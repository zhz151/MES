using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IMetallographicTestService
{
    Task<PagedResult<MetallographicTestDto>> GetAllAsync(QueryParams query);
    Task<MetallographicTestDto?> GetByIdAsync(int id);
    Task<MetallographicTestDto> CreateAsync(CreateMetallographicTestRequest request);
    Task<MetallographicTestDto> UpdateAsync(int id, UpdateMetallographicTestRequest request);
    Task DeleteAsync(int id);
    Task<List<MetallographicTestDto>> BatchCreateAsync(List<CreateMetallographicTestRequest> requests);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
