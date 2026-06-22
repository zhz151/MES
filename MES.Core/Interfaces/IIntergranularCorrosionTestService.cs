using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IIntergranularCorrosionTestService
{
    Task<PagedResult<IntergranularCorrosionTestDto>> GetAllAsync(QueryParams query);
    Task<IntergranularCorrosionTestDto?> GetByIdAsync(int id);
    Task<IntergranularCorrosionTestDto> CreateAsync(CreateIntergranularCorrosionTestRequest request);
    Task<IntergranularCorrosionTestDto> UpdateAsync(int id, UpdateIntergranularCorrosionTestRequest request);
    Task DeleteAsync(int id);
    Task<List<IntergranularCorrosionTestDto>> BatchCreateAsync(List<CreateIntergranularCorrosionTestRequest> requests);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
