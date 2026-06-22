using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IHardnessTestService
{
    Task<PagedResult<HardnessTestDto>> GetAllAsync(QueryParams query);
    Task<HardnessTestDto?> GetByIdAsync(int id);
    Task<HardnessTestDto> CreateAsync(CreateHardnessTestRequest request);
    Task<HardnessTestDto> UpdateAsync(int id, UpdateHardnessTestRequest request);
    Task DeleteAsync(int id);
    Task<List<HardnessTestDto>> BatchCreateAsync(List<CreateHardnessTestRequest> requests);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
