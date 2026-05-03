using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IMaterialService
{
    Task<PagedResult<MaterialDto>> GetPagedAsync(QueryParams query);
    Task<MaterialDto> GetByIdAsync(int id);
    Task<List<MaterialDto>> GetActiveAsync();
    Task<List<string>> GetCategoriesAsync();
    Task<MaterialDto?> MatchAsync(string category, string grade, string spec);
    Task<MaterialDto> CreateAsync(CreateMaterialRequest request);
    Task<MaterialDto> UpdateAsync(int id, UpdateMaterialRequest request);
    Task DeleteAsync(int id);
}
