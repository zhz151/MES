using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IOrderService
{
    Task<PagedResult<SalesOrderListDto>> GetPagedAsync(QueryParams query);

    Task<SalesOrderDetailDto> GetByIdAsync(int id);

    Task<SalesOrderListDto> CreateAsync(CreateSalesOrderRequest request);

    Task<SalesOrderListDto> UpdateAsync(int id, UpdateSalesOrderRequest request);

    Task DeleteAsync(int id);
}