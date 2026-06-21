using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IGradeChemicalCompositionService
{
    Task<PagedResult<GradeChemicalCompositionDto>> GetPagedAsync(QueryParams query);
    Task<List<GradeChemicalCompositionDto>> GetAllAsync();
    Task<GradeChemicalCompositionDto> GetByIdAsync(int id);
    Task<GradeChemicalCompositionDto> CreateAsync(CreateGradeChemicalCompositionRequest request);
    Task<GradeChemicalCompositionDto> UpdateAsync(int id, UpdateGradeChemicalCompositionRequest request);
    Task DeleteAsync(int id);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
