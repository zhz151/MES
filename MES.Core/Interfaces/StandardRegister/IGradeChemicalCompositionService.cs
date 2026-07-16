using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.StandardRegister;
namespace MES.Core.Interfaces.StandardRegister;

public interface IGradeChemicalCompositionService
{
    Task<PagedResult<GradeChemicalCompositionDto>> GetPagedAsync(QueryParams query);
    Task<List<GradeChemicalCompositionDto>> GetAllAsync();
    Task<GradeChemicalCompositionDto> GetByIdAsync(int id);
    Task<GradeChemicalCompositionDto> CreateAsync(CreateGradeChemicalCompositionRequest request);
    Task<GradeChemicalCompositionDto> UpdateAsync(int id, UpdateGradeChemicalCompositionRequest request);
    Task DeleteAsync(int id);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns);
}
