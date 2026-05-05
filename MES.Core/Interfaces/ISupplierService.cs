using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface ISupplierService
{
    Task<PagedResult<SupplierProfileDto>> GetPagedAsync(QueryParams query);
    Task<SupplierProfileDto> GetByIdAsync(int id);
    Task<List<SupplierProfileDto>> GetActiveAsync();
    Task<SupplierProfileDto> CreateAsync(CreateSupplierRequest request);
    Task<List<SupplierProfileDto>> CreateBatchAsync(List<CreateSupplierRequest> requests);
    Task<SupplierProfileDto> UpdateAsync(int id, UpdateSupplierRequest request);
    Task DeleteAsync(int id);

    // ========== 打印 ==========
    Task<byte[]> PrintSupplierAsync(int id);
    Task<byte[]> PrintSupplierBatchAsync(int[] ids);
    Task<byte[]> PrintSupplierAllAsync(string? keyword, string? sortBy = null, bool isDescending = false);
}
