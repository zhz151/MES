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
}