using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IFlatteningTestService
{
    Task<PagedResult<FlatteningTestDto>> GetAllAsync(QueryParams query);
    Task<FlatteningTestDto?> GetByIdAsync(int id);
    Task<FlatteningTestDto> CreateAsync(CreateFlatteningTestRequest request);
    Task<FlatteningTestDto> UpdateAsync(int id, UpdateFlatteningTestRequest request);
    Task DeleteAsync(int id);
    Task<List<FlatteningTestDto>> BatchCreateAsync(List<CreateFlatteningTestRequest> requests);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
