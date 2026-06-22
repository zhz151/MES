using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IGrainSizeTestService
{
    Task<PagedResult<GrainSizeTestDto>> GetAllAsync(QueryParams query);
    Task<GrainSizeTestDto?> GetByIdAsync(int id);
    Task<GrainSizeTestDto> CreateAsync(CreateGrainSizeTestRequest request);
    Task<GrainSizeTestDto> UpdateAsync(int id, UpdateGrainSizeTestRequest request);
    Task DeleteAsync(int id);
    Task<List<GrainSizeTestDto>> BatchCreateAsync(List<CreateGrainSizeTestRequest> requests);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
