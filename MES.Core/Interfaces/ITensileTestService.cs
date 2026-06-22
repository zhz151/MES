using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface ITensileTestService
{
    Task<PagedResult<TensileTestDto>> GetAllAsync(QueryParams query);
    Task<TensileTestDto?> GetByIdAsync(int id);
    Task<TensileTestDto> CreateAsync(CreateTensileTestRequest request);
    Task<TensileTestDto> UpdateAsync(int id, UpdateTensileTestRequest request);
    Task DeleteAsync(int id);
    Task<List<TensileTestDto>> BatchCreateAsync(List<CreateTensileTestRequest> requests);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
