using MES.Core.DTOs.Shared;
using MES.Core.DTOs.StandardRegister;
using MES.Core.Models;

namespace MES.Core.Interfaces.StandardRegister;

/// <summary>
/// 工厂检验项要求 Service 接口
/// </summary>
public interface IFactoryInspectionRequirementService
{
    Task<PagedResult<FactoryInspectionRequirementDto>> GetPagedAsync(QueryParams query);
    Task<FactoryInspectionRequirementDto?> GetByIdAsync(int id);
    Task<FactoryInspectionRequirementDto> CreateAsync(CreateFactoryInspectionRequirementRequest request);
    Task<FactoryInspectionRequirementDto> UpdateAsync(int id, UpdateFactoryInspectionRequirementRequest request);
    Task DeleteAsync(int id);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>批量打印选中记录</summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>按条件打印全部记录</summary>
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns);
}
