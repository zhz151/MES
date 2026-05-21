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

    /// <summary>
    /// 获取所有客户（无分页，供客户端筛选排序）
    /// </summary>
    Task<List<CustomerProfileDto>> GetAllListAsync();

    /// <summary>
    /// 获取客户下拉列表（仅含级联选择所需字段，不分页）
    /// </summary>
    Task<List<CustomerSelectDto>> GetSelectListAsync();

    // ========== 打印 ==========
    Task<byte[]> PrintCustomerAsync(int id);
    Task<byte[]> PrintCustomerBatchAsync(int[] ids);
    Task<byte[]> PrintCustomerAllAsync(string? keyword, string? sortBy = null, bool isDescending = false);
}