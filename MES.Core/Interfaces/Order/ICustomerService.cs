using MES.Core.Models;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.Shared;
namespace MES.Core.Interfaces.Order;
public interface ICustomerService
{
    Task<PagedResult<CustomerProfileDto>> GetPagedAsync(QueryParams query);

    Task<CustomerProfileDto> GetByIdAsync(int id);

    Task<CustomerProfileDto> CreateAsync(CreateCustomerRequest request);

    Task<CustomerProfileDto> UpdateAsync(int id, UpdateCustomerRequest request);

    Task DeleteAsync(int id);

    /// <summary>
    /// 获取客户下拉列表（仅含级联选择所需字段，不分页）
    /// </summary>
    Task<List<CustomerSelectDto>> GetSelectListAsync();

    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    // ========== 打印 ==========
    Task<byte[]> PrintCustomerBatchAsync(int[] ids, List<PrintColumnDef>? columns = null);
}
