using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.StandardRegister;
namespace MES.Core.Interfaces.StandardRegister;

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

    /// <summary>批量打印选中记录</summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>按条件打印全部记�?/summary>
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns);
}
