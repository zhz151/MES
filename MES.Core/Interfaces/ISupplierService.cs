using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface ISupplierService
{
    Task<PagedResult<SupplierProfileDto>> GetPagedAsync(QueryParams query);
    Task<List<SupplierProfileDto>> GetAllListAsync();
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
    Task<byte[]> PrintSupplierAsync(int id);
    Task<byte[]> PrintSupplierBatchAsync(int[] ids);
    Task<byte[]> PrintSupplierAllAsync(string? keyword, string? sortBy = null, bool isDescending = false);
}
