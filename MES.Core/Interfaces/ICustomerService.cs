using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;
public interface ICustomerService
{
    Task<PagedResult<CustomerProfileDto>> GetPagedAsync(QueryParams query);

    Task<CustomerProfileDto> GetByIdAsync(int id);

    Task<CustomerProfileDto> CreateAsync(CreateCustomerRequest request);

    Task<CustomerProfileDto> UpdateAsync(int id, UpdateCustomerRequest request);

    Task DeleteAsync(int id);

    // ========== 打印 ==========
    Task<byte[]> PrintCustomerAsync(int id);
    Task<byte[]> PrintCustomerBatchAsync(int[] ids);
    Task<byte[]> PrintCustomerAllAsync(string? keyword, string? sortBy = null, bool isDescending = false);
}