using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 标准号检验项要求 Service 接口
/// </summary>
public interface IStandardInspectionRequirementService
{
    Task<PagedResult<StandardInspectionRequirementDto>> GetPagedAsync(QueryParams query);
    Task<StandardInspectionRequirementDto?> GetByIdAsync(int id);
    Task<StandardInspectionRequirementDto> CreateAsync(CreateStandardInspectionRequirementRequest request);
    Task<StandardInspectionRequirementDto> UpdateAsync(int id, UpdateStandardInspectionRequirementRequest request);
    Task DeleteAsync(int id);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
