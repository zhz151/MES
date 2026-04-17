using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;


public interface ICustomerService
{

    Task<ApiResponse<PagedResult<CustomerProfileDto>>> GetPagedAsync(QueryParams query);


    Task<List<CustomerProfileDto>> GetAllAsync();


    Task<ApiResponse<CustomerProfileDto>> GetByIdAsync(int id);

    Task<ApiResponse<CustomerProfileDto>> CreateAsync(CreateCustomerRequest request);


    Task<ApiResponse<CustomerProfileDto>> UpdateAsync(int id, UpdateCustomerRequest request);

    Task<ApiResponse<object>> DeleteAsync(int id);
}