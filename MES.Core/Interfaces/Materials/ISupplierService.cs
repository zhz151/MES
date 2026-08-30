using MES.Core.Models;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Shared;
namespace MES.Core.Interfaces.Materials;

public interface ISupplierService
{
    Task<PagedResult<SupplierProfileDto>> GetPagedAsync(QueryParams query);
    Task<SupplierProfileDto> GetByIdAsync(int id);
    Task<List<SupplierProfileDto>> GetActiveAsync();
    Task<SupplierProfileDto> CreateAsync(CreateSupplierRequest request);
    Task<List<SupplierProfileDto>> CreateBatchAsync(List<CreateSupplierRequest> requests);
    Task<SupplierProfileDto> UpdateAsync(int id, UpdateSupplierRequest request);
    Task DeleteAsync(int id);

    /// <summary>
    /// 获取筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    // ========== 打印 ==========
    Task<byte[]> PrintSupplierBatchAsync(int[] ids, List<PrintColumnDef>? columns = null);
}
