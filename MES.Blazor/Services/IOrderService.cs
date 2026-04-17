using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public interface IOrderService
{

    Task<ApiResponse<PagedResult<SalesOrderListDto>>> GetPagedAsync(QueryParams query);

    Task<ApiResponse<SalesOrderDetailDto>> GetByIdAsync(int id);


    Task<ApiResponse<SalesOrderListDto>> CreateAsync(CreateSalesOrderRequest request);

    Task<ApiResponse<SalesOrderListDto>> UpdateAsync(int id, UpdateSalesOrderRequest request);

    Task<ApiResponse<object>> DeleteAsync(int id);
}