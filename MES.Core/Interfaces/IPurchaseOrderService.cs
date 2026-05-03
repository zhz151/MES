using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IPurchaseOrderService
{
    Task<PagedResult<PurchaseOrderDto>> GetPagedAsync(PurchaseOrderQueryParams query);
    Task<PurchaseOrderDto> GetByIdAsync(int id);
    Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request);
    Task<PurchaseOrderDto> UpdateAsync(int id, UpdatePurchaseOrderRequest request);
    Task SyncAllAsync();
    Task SyncSingleAsync(int id);
    Task UpdateStatusAsync(int id, UpdateOrderStatusRequest request);
    Task DeleteAsync(int id);
}
