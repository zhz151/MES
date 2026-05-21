using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IPurchaseOrderService
{
    Task<PagedResult<PurchaseOrderDto>> GetPagedAsync(PurchaseOrderQueryParams query);
    Task<List<PurchaseOrderDto>> GetAllListAsync();
    Task<PurchaseOrderDto> GetByIdAsync(int id);
    Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request);
    Task<List<PurchaseOrderDto>> CreateBatchAsync(List<CreatePurchaseOrderRequest> requests);
    Task<PurchaseOrderDto> UpdateAsync(int id, UpdatePurchaseOrderRequest request);
    Task SyncAllAsync();
    Task SyncSingleAsync(int id);
    Task UpdateStatusAsync(int id, UpdateOrderStatusRequest request);
    Task DeleteAsync(int id);
    Task<List<ProcurementStatusDto>> GetProcurementStatusAsync();
    Task<List<ProcurementStatusDto>> GetPiercingProcurementStatusAsync();
    Task<List<OrderMismatchInfo>> GetMismatchedPurchaseOrdersAsync();
    Task<PlanDetailDto?> GetPlanDetailAsync(string workOrderNo, string materialCategory);

    // ========== 打印 ==========
    Task<byte[]> PrintOrderAsync(int id);
    Task<byte[]> PrintOrderBatchAsync(int[] ids);
    Task<byte[]> PrintOrderAllAsync(string? keyword, string? sortBy = null, bool isDescending = false);
}
